# Schema Versioning Policy

This document defines two versioned payload families for ShapeForge artifacts:

1. **Diagnostics schema** (`diagnosticsVersion`)
2. **Recipe/PEM schema** (`recipeVersion`)

The intent is to guarantee safe reads of older payloads, deterministic rewrites to the current canonical schema, and predictable deprecation windows.

---

## 1) Diagnostics Payload Family (`diagnosticsVersion`)

### Canonical current version
- **Current**: `diagnosticsVersion = "1.0"`.
- Internally, this corresponds to `MeshDiagnostics.SchemaVersion` populated by `ReportCard.SchemaVersion`.

### Required fields
- `diagnosticsVersion` (string semver-like, e.g. `"1.0"`)
- `topology` (object of numeric metrics)
- `quality` (object of numeric metrics)
- `printability` (object of numeric metrics; may be empty)
- `issues` (array of diagnostic issue entries)

### Optional fields
- `counts` (object of integer counters)
- `booleans` (object of boolean flags)
- Additional namespaced metric groups (future extension), provided unknown groups are ignored by readers.

### Backward-compatible change rules
Allowed in a minor/patch-compatible evolution:
- Add new optional metric keys under existing maps.
- Add new optional top-level groups.
- Add new optional properties to each `issue` object.
- Broaden enum-like value sets when consumers treat unknown values as non-fatal.

Not backward-compatible (requires major version bump):
- Removing or renaming required fields.
- Changing field types (e.g., number -> string).
- Reinterpreting metric semantics in a way that changes meaning for the same key.

### Deprecation strategy
- Mark deprecated keys in docs first and keep readers accepting them for **at least one minor release**.
- Writers should stop emitting deprecated keys immediately after introducing replacements.
- Readers should map deprecated keys to canonical keys during normalization and emit a deprecation note in logs/diagnostics output.

### Migration function location
- Place migration/normalization functions in:
  - `src/ShapeForge.Core/Pipeline/SchemaMigrations/Diagnostics/`
- Suggested entry point:
  - `DiagnosticsSchemaMigrator.NormalizeToCurrent(JsonElement payload)`

### Test expectations
- Add/maintain tests that:
  1. Load a `diagnosticsVersion: "1.0"` payload and preserve semantic values.
  2. Load payloads containing deprecated keys and normalize them to canonical keys.
  3. Round-trip through canonical writer and verify output is rewritten with the current `diagnosticsVersion` and canonical field names/order-insensitive equality.
  4. Reject unsupported major versions with clear errors.

---

## 2) Recipe/PEM Payload Family (`recipeVersion`)

### Canonical current version
- **Current**: `recipeVersion = 2`.
- The runtime model currently uses `RecipeDocument.Version` and migrates v1 payloads to v2.
- Canonical persisted payloads should be v2-equivalent and include profile/recipe/pem structure.

### Required fields
- `recipeVersion` (integer)
- `recipe` (object)
  - `steps` (array of recipe step objects)
- For each step:
  - `op` (string operator id)
  - `params` (object; can be empty)

### Optional fields
- `profile` (object of profile overrides)
- `pem` (object)
  - `name` (string)
  - `defaults` (profile object)
  - `recipe` (recipe object)
  - `validation` (validation rule set)
- `recipe.operatorOverrides` (map by operator id)
- Additional optional profile fields may be added in future versions.

### Backward-compatible change rules
Allowed without major bump:
- Adding optional profile fields.
- Adding optional PEM validation rules with safe defaults.
- Adding optional operator-level metadata ignored by existing loaders.
- Accepting legacy aliases (for example, legacy root `version` during read) while rewriting to canonical current form.

Not backward-compatible:
- Renaming/removing required step fields (`op`, `params`).
- Changing field types for required fields.
- Altering inheritance precedence (`base -> profile -> pem.defaults -> runtime`) without version bump.

### Deprecation strategy
- Keep deserializers tolerant of deprecated fields for **one minor release minimum**.
- Emit canonical payloads only (current version + canonical field names).
- Track deprecations in this file with:
  - first deprecated release,
  - planned removal release,
  - migration mapping.

### Migration function location
- Existing migration logic lives in `RecipeDocument.FromJson`.
- New explicit migrators should live in:
  - `src/ShapeForge.Core/Pipeline/SchemaMigrations/Recipes/`
- Suggested entry point:
  - `RecipeSchemaMigrator.NormalizeToCurrent(JsonElement payload)`

### Test expectations
- Add/maintain tests that:
  1. Load legacy v1 recipe payloads and map to v2 runtime model.
  2. Rewrite any legacy payload to canonical current version when serializing (`ToJson`/normalizer path).
  3. Verify canonical output no longer contains legacy-only fields and uses current structure.
  4. Reject unsupported future/unknown major versions with actionable error messages.

---

## Cross-family invariants
- Readers must be **more permissive** than writers.
- Writers must emit **only canonical current** shape.
- Migration must be deterministic and idempotent:
  - `normalize(normalize(payload)) == normalize(payload)`
- Unsupported versions must fail fast with explicit version and accepted-range guidance.


## Contract constants

- Diagnostics schema constant: `MeshDiagnostics.CurrentSchemaVersion` (`1.0`).
- Recipe/PEM schema constant: `RecipeDocument.CurrentVersion` (`2`).
- Operator schema baseline constant: `OperatorSchema.CurrentSchemaVersion` (`1.0`).
- Pipeline run payload constant: `PipelineRunResult.CurrentSchemaVersion` (`1.0`).

Migration hook entry points:

- `DiagnosticsSchemaMigrator.NormalizeToCurrent(JsonElement payload)`
- `RecipeSchemaMigrator.NormalizeToCurrent(JsonElement payload)`
