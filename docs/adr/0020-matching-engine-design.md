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
- $\text{expiryFactor} \in [0, 1]$: FEFO (First-Expired, First-Out) preference. Units closer to expiration score higher: $1 - (\text{hoursRemaining} / \text{maxShelfLifeHours})$.
- $\text{distanceFactor} \in [0, 1]$: Proximity preference based on Haversine distance: $\max(0, 1 - \text{distanceKm} / \text{maxRelevantKm})$.

The aggregate score is computed as:
$$\text{score} = w_{\text{waste}} \times (1 - \text{waste}) + w_{\text{expiry}} \times \text{expiryFactor} + w_{\text{distance}} \times \text{distanceFactor}$$

Weight profiles are pure data structures selected by request `Urgency`:
- **Routine:** $w_{\text{waste}} = 0.50,\; w_{\text{expiry}} = 0.40,\; w_{\text{distance}} = 0.10$ (minimizes rare blood wastage and expired discard).
- **Urgent:** $w_{\text{waste}} = 0.35,\; w_{\text{expiry}} = 0.30,\; w_{\text{distance}} = 0.35$ (balanced allocation).
- **Critical:** $w_{\text{waste}} = 0.15,\; w_{\text{expiry}} = 0.10,\; w_{\text{distance}} = 0.75$ (prioritizes speed and shortest delivery distance).

### 3. Deterministic Tie-Breaking
Matching must be 100% repeatable given identical inputs:
1. Higher average score wins.
2. Earlier earliest-expiry among chosen units wins.
3. Lower `BankId` (and `UnitId`) wins as deterministic tie-breaker.

### 4. Deliberately Left Out (Out of Scope for MVP)
- **Multi-bank splitting:** Fulfilling a single request from multiple banks requires multi-courier coordination and partial reservation compensation, which adds excessive complexity for the MVP.
- **Dynamic traffic routing APIs:** Distances use straight-line Haversine with a configurable average speed (40 km/h) rather than Google Maps/OSRM.
- **Dynamic waste-fade near expiry:** In the current formula, weights are static per profile. Whether waste penalty should dynamically fade to zero as a rare unit approaches expiration is documented as a tuning consideration for future phases.

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
- **Static weights:** Near-expiry universal donor units (O-) might still be penalized on waste under `Routine` urgency unless the expiry weight overcomes the waste penalty.
