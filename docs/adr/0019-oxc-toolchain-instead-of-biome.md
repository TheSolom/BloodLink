# ADR-0019: Adopt Oxc Toolchain (oxlint + oxfmt) for Web Dashboard

* **Status:** Accepted
* **Date:** 2026-11-06
* **Decision Makers:** Fullstack & Frontend Engineering

---

## Context

The BloodLink web dashboard (`web/dashboard`) previously utilized [Biome](https://biomejs.dev/) as its unified linter, formatter, and import organizer. While Biome provided initial speed and consolidation, our frontend stack heavily depends on ecosystem-standard React, TypeScript, Vite, and TanStack Query patterns.

We needed a toolchain that satisfies several requirements:
1. Exceptional execution speed across local pre-commit hooks and CI pipelines.
2. Full coverage of essential lint categories: React hooks (`rules-of-hooks`, `exhaustive-deps`), accessibility (`jsx-a11y`), TypeScript semantics, and Vitest test practices.
3. Import sorting and canonical formatting compatible with existing conventions.
4. Support for specialized domain plugins—specifically `@tanstack/eslint-plugin-query`—without requiring full fallback to legacy, slow ESLint setups.
5. Clear separation between AST linting and authoritative type checking (`tsc --noEmit`).

The [Oxc](https://oxc.rs/) project provides dedicated, Rust-native tools: `oxlint` (high-performance linter with native plugin support and an ESLint v9+ JavaScript plugin bridge) and `oxfmt` (high-performance formatter with built-in import sorting).

## Decision

We migrate `web/dashboard` from Biome to the Oxc toolchain:

1. **Toolchain & Version Pinning:**
   - Install `oxlint` pinned at `1.92.0` and `oxfmt` pinned at `0.77.0` as exact `devDependencies`.
   - Do not install `oxlint-tsgolint`; TypeScript compilation and type-checking remain exclusively governed by `tsc --noEmit`.

2. **Linter Configuration (`.oxlintrc.json`):**
   - Enabled built-in plugins: `typescript`, `react` (covering React Hook rules `react/rules-of-hooks` and `react/exhaustive-deps`), `jsx-a11y`, `import`, and `vitest`.
   - Set `"categories": { "correctness": "error" }`.
   - Set `"jsx-a11y/prefer-tag-over-role": "off"` to preserve the existing accessibility semantics previously configured under Biome's `useSemanticElements: "off"`.
   - Ignore paths matching project boundaries: `dist/**`, `src/shared/api/generated/**`, `src/shared/ui/**` (shadcn components), `openapi.json`, `package-lock.json`, `*.tsbuildinfo`, `tsbuildinfo`.

3. **TanStack Query Plugin Findings (Step 4 Investigation):**
   - We tested `@tanstack/eslint-plugin-query` (v5.106.1) through Oxlint's JavaScript plugin (`jsPlugins`) bridge.
   - The plugin functions reliably within Oxlint: rules such as `@tanstack/query/exhaustive-deps`, `@tanstack/query/stable-query-client`, and `@tanstack/query/no-rest-destructuring` execute directly, properly catching dependency omissions in `queryKey` definitions during automated lint runs.
   - Enabled directly in `.oxlintrc.json` under `jsPlugins` with alias `@tanstack/query`.

4. **Formatter Configuration (`.oxfmtrc.json`):**
   - Standardized formatting rules matching project standards: 2-space indentation, 100 print width, double quotes, and semicolons.
   - Enabled import sorting via `"sortImports": true` (leveraging Oxfmt's built-in perfectionist-style import sorter).
   - Applied identical ignore patterns as `.oxlintrc.json`.

5. **NPM Scripts:**
   - `lint`: `oxlint`
   - `lint:fix`: `oxlint --fix`
   - `format`: `oxfmt --write`
   - `format:check`: `oxfmt --check`
   - `typecheck`: `tsc --noEmit`
   - `check`: `oxlint && oxfmt --check && tsc --noEmit` (read-only validation for CI and local verification)
   - `fix`: `oxlint --fix && oxfmt --write`

6. **Tooling & CI Integrations:**
   - **VS Code:** Configured root `.vscode/settings.json` and `.vscode/extensions.json` recommending `oxc.oxc-vscode`, binding format-on-save and linter paths to `web/dashboard`.
   - **Lefthook:** Pre-commit hook updated to run `oxfmt --write --no-error-on-unmatched-pattern` and `oxlint --no-error-on-unmatched-pattern`.
   - **CI:** GitHub Actions `web` job executes `npm ci`, `npm run check`, `npm test`, and `npm run build`.
   - **Dependabot:** `oxlint` and `oxfmt` are explicitly excluded from automated grouped dependency updates (`exclude-patterns`) to prevent unexpected formatting churn.

## Alternatives Considered

- **Retain Biome:** Biome's monolithic architecture lacks native or bridged support for ecosystem-specific lint plugins like `@tanstack/eslint-plugin-query`, requiring either custom rule workarounds or missing out on query dependency validation.
- **Traditional ESLint + Prettier:** While providing broad plugin support, execution time in CI and developer feedback loops is an order of magnitude slower than Rust-based tooling.
- **Oxlint with `oxlint-tsgolint`:** Type-aware linting adds significant initial overhead and duplicate processing while TypeScript compiler diagnostics (`tsc --noEmit`) already ensure strict type safety.

## Consequences

### Positive
- **Drastic Performance Improvement:** `oxlint` finishes analysis in under 50ms (and ~1s on cold runs), and `oxfmt` verifies or writes formatting across all files in under 30ms.
- **TanStack Query Validation:** Full automated lint enforcement for React Query query keys and client usage via `@tanstack/eslint-plugin-query`.
- **Ecosystem Alignment:** Clean integration with ESLint v9 flat-plugin standards, standard VS Code LSP extension, and Lefthook pre-commit workflows.

### Negative / Trade-offs
- **Pre-1.0 Formatter Evolution:** `oxfmt` is currently pre-1.0 (`0.77.0`). Minor releases may introduce subtle formatting adjustments or AST formatting refinements.
  - *Mitigation:* `oxfmt` and `oxlint` versions are strictly pinned in `package.json`, excluded from automatic Dependabot grouping, and will only be upgraded intentionally with dedicated formatting review commits.
- **Experimental JS Plugin Bridge:** Oxlint's `jsPlugins` capability is in active evolution. Future Oxlint releases may adjust JS plugin bridge syntax or performance characteristics.
