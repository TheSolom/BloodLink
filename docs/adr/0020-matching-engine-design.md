# ADR-0020: Matching Engine Design

* **Status:** Accepted
* **Date:** 2026-11-15
* **Decision Makers:** Core Engineering Team

---

## Context

Blood matching in logistics is not standard e-commerce inventory lookup:
- Blood components expire rapidly (platelets in 5 days, red cells in 42 days).
- Biological compatibility (ABO and Rh factor) strictly restricts which donors can safely give to which recipients.
- Medical urgency varies dramatically: a routine scheduled surgery can wait for geographically optimal stock and preserve rare universal donors, whereas trauma / critical hemorrhage requires immediate delivery from the closest compatible bank regardless of donor rarity.
- Concurrency and fulfillment require clear rules on multi-bank sourcing and trip viability.

## Decision

We decide to implement the matching engine within `Requests.Domain` using a two-stage evaluation pipeline: **Hard Filters** followed by **Normalized Soft Scoring** driven by data-driven urgency weight profiles.

### 1. Hard Filters (Non-Negotiable Pre-Conditions)
Hard filters are never relaxed, even under `Critical` urgency:
1. **Component Match:** Unit component must strictly match requested component (`RedCells` or `Platelets`).
2. **Biological Compatibility:** Donor blood type must be compatible with recipient blood type (evaluated via `RedCellCompatibility` table for red cells, or `PlateletCompatibility` for platelets).
3. **Exclusion Check:** Unit must not be in the request's `excludedUnitIds` set (e.g. previously rejected units).
4. **Transit Survival Window:** `ExpiresAt > now + estimatedTravelTime + arrivalSafetyMargin`. Units that would expire in transit or upon arrival are discarded.
5. **Single-Bank Sourcing (MVP Rule):** A single blood bank must be able to fill the entire request (`unitsNeeded`). Requests are not split across multiple banks in the MVP.

### 2. Soft Scoring and Weight Profiles
For candidate banks meeting all hard filters, each unit is evaluated across three factors normalized to $[0, 1]$:
- $\text{waste} \in [0, 1]$: measures the sacrifice of donor utility. For red cells, $\text{waste} = (\text{donorServesCount} - \text{recipientOwnServesCount}) / 7$. For platelets, $\text{waste} = 0$ for exact ABO match, $1$ otherwise. Lower waste is better, scored as $(1 - \text{waste})$.
- $\text{expiryFactor} \in [0, 1]$: Window-based FEFO (First-Expired, First-Out) preference.
  $$\text{window} = \text{ExpiryWindowFraction} \times \text{ShelfLife}$$
  $$\text{expiryFactor} = 1 - \operatorname{clamp}\left(\frac{\text{remainingTime}}{\text{window}}, 0, 1\right)$$
  Configured via `MatchingOptions` (default `ExpiryWindowFraction = 0.20`, yielding $\sim 8.4$ days for red cells, $1$ day for platelets).
  - Outside the window ($\text{remainingTime} \ge \text{window}$): $\text{expiryFactor} = 0$. Fresh units do not receive an expiry bonus, so waste penalty prevents scarce universal donors (O-) from being wasted when non-scarce stock (O+) is available.
  - Inside the window ($\text{remainingTime} < \text{window}$): $\text{expiryFactor}$ rises linearly toward 1. This naturally overcomes the waste penalty under Routine urgency, rescuing units from shelf expiration without needing an artificial or arbitrary waste-fade curve.
- $\text{distanceFactor} \in [0, 1]$: Proximity preference based on Haversine distance: $\max(0, 1 - \text{distanceKm} / \text{maxRelevantKm})$.

The aggregate score is computed as:
$$\text{score} = w_{\text{waste}} \times (1 - \text{waste}) + w_{\text{expiry}} \times \text{expiryFactor} + w_{\text{distance}} \times \text{distanceFactor}$$

All parameters (weights $w$, window fraction, travel speed, arrival safety margin, max distance) are loaded dynamically from `MatchingOptions`, not hardcoded constants.

Weight profiles are pure data structures selected by request `Urgency`:
- **Routine:** $w_{\text{waste}} = 0.50,\; w_{\text{expiry}} = 0.40,\; w_{\text{distance}} = 0.10$ (protects rare donor units; rescues near-expiry units).
- **Urgent:** $w_{\text{waste}} = 0.35,\; w_{\text{expiry}} = 0.30,\; w_{\text{distance}} = 0.35$ (balanced clinical allocation).
- **Critical:** $w_{\text{waste}} = 0.15,\; w_{\text{expiry}} = 0.10,\; w_{\text{distance}} = 0.75$ (prioritizes speed and shortest delivery distance).

### 3. Deterministic Tie-Breaking
Matching must be 100% repeatable given identical inputs:
1. Higher average score wins.
2. Earlier earliest-expiry among chosen units wins.
3. Lower `BankId` (and `UnitId`) wins as deterministic tie-breaker.

### 4. Deliberately Left Out (Out of Scope for MVP)
- **Multi-bank splitting:** Fulfilling a single request from multiple banks requires multi-courier coordination and partial reservation compensation, which adds excessive complexity for the MVP.
- **Dynamic traffic routing APIs:** Distances use straight-line Haversine with a configurable average speed (40 km/h) rather than external mapping services.

---

## Future Work

### Multi-Bank Fulfillment
In the MVP, if Bank A has 2 units and Bank B has 2 units, a 3-unit request fails with `NoBankCanFillEntireRequest`. Multi-bank fulfillment is planned for post-MVP and will require:
1. **Planner Pass:** A knapsack / combinatorial search pass over candidate banks to find the minimal-bank combination that satisfies `unitsNeeded` while optimizing composite distance and shelf-life scores.
2. **Saga Orchestration & Compensation:** The request reservation saga must issue parallel or sequential reservation requests across multiple blood banks. If Bank A reserves 2 units but Bank B fails to reserve the remaining 1 unit (due to concurrent lock or out-of-stock), the saga must execute compensating actions (release reserved units at Bank A) or attempt a secondary match.
3. **Dispatch Courier Impact:** Dispatch cannot dispatch a single courier. It must either coordinate multiple concurrent courier dispatches (one per origin bank) or plan a multi-stop pickup route, significantly increasing operational coordination and delivery tracking complexity.

## Alternatives Considered

- **Separate matching policy classes per urgency (`NearestFirstPolicy`, `ExpiryFirstPolicy`):**
  - *Rejected:* Hardcodes rigid behavioral silos. The unified weighted scoring formula parameterized by weight profiles handles all cases cleanly and allows configuration tuning without code changes.
- **Constraint satisfaction solvers (e.g. OptaPlanner / Google OR-Tools):**
  - *Rejected:* Heavy external runtime dependency for an MVP where inventory per region can be sorted and scored in sub-millisecond memory scans.

## Consequences

### Positive
- **Predictable, verifiable decisions:** Clear separation between hard rules (medical safety, trip viability) and soft heuristics (optimization).
- **Testable by design:** Weight profiles and scoring functions are pure domain logic without side effects, tested under realistic clinical scenarios.
- **Extensible:** Additional profiles or dynamic weight tuning can be added without altering the matching pipeline.

### Negative / Trade-offs
- **Single bank limitation:** If Bank A has 2 units and Bank B has 2 units, a 3-unit request fails with `NoBankCanFillEntireRequest`. Multi-bank fulfillment is deferred to future work.
- **Window tuning:** The window-based expiry calculation (`ExpiryWindowFraction`) naturally rescues near-expiry units before waste penalties can cause spoilage, but requires calibrated window sizing (e.g. 20%) to balance donor conservation against discard risk.
