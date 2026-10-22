# Spec: Recipes, shown to people as "Workflows" (Avalonia build spec)

Screen `recipes` (the rail's Recipes, chord G R; the page title is "Workflows" for templates, the Workflows page is "runs"; see open item 1). Inputs: `screens/recipes.html` (built by `data/tools/pg_recipes.py`), `screens/recipes.notes.md`, `data/PLAIN-WORDS.md`, `data/DATA-MODEL.md` section 1, screens spec section 4.3. Code read at origin/main: `src/Sindri/Aw.Sindri.Ui/{Views/Recipes,ViewModels/Recipes,ViewModels/Labels.Recipes.cs,ViewModels/Labels.App.cs,ViewModels/Shell/ShellViewModel.Recipe.cs,Api/Workflow}`, endpoints `ListRecipesEndpoint` and `ShowRecipeEndpoint`, goldens `tests/Aw.Sindri.Tests/Golden/Api/{workflow-recipes,workflow-recipe}/*.json`.

Today the Recipes tab is a placeholder ("arrives with U10 and the recipe stats (API P12)", `Labels.ArrivesRecipes`); only the per-recipe sheet ("How it works") exists, opened from a run. The list is served (`GET /workflow/recipes`), so this spec builds the list now and leaves the usage statistics for P12.

## 1. Goal and decisions

One question: which workflow templates exist, which are approved, and what does each one do.

| Q | Decision | Effect |
|---|---|---|
| 1 | The page lists every template with a plain name and the id in grey mono, version, one standing word (Approved, Not approved, Revoked), size, "n in, n out" and a short fingerprint | `RecipesPageViewModel` over `ListRecipes`; placeholder page is replaced |
| 2 | A row opens the 480 px sheet ("How it works") with the steps as chips in order, the loops in words, What it needs, What it produces, Details | the existing `RecipeSheetViewModel` content, restyled; the sheet stays the shell sheet |
| 3 | Standing is a badge; "out of date" (stale `pin-drift`, `child-stale`, `legacy-shape`) is a second grey word with the reason in the sheet | from `stale[]` |
| 4 | Filters: All and Needs approval (not approved, revoked or out of date) with counts; search by name or id | client filter |
| 5 | Approving one template is a typed confirmation, terminal copy until the setup-once spec builds it; "Approve new workflows" links to Settings where the held approve-all lives | `ProtectedActionButton`, deep link `setup#approveall` |
| 6 | Runs, success rate and average time per workflow are NOT shown: not served (API P12). The footer says so | no stub numbers |

**Not built (prototype only):** the rail, the 56 px bar, "View as", Annotations. The shell keeps its 48 px bar.

## 2. Regions and layout

| Region | Size and tokens | Avalonia |
|---|---|---|
| Head | `page-title` "Workflows", caption "12 workflows · 11 approved"; right: Approve new workflows (`Primary`) | head `Grid` |
| Toolbar | 48 px: segmented All and Needs approval, key hints | segmented `ListBox` |
| Table | `Surface1` card; header 28 px; rows 36 px; Workflow flexible 2fr (name 500, id grey mono), Version 70, Standing 170, Size 80, Needs and produces 90, Fingerprint 100; under 780 px: Workflow, Standing, Fingerprint | `ItemsControl` of `RecipeRow` |
| Sheet | header badge and version, plain name 16/600, one description sentence, copy chip of the id; callouts for Not approved and Out of date; "How it works" step chips (24 px, 6 px gap, wrapped) with the loops under; What it needs; What it produces; Details | existing `RecipeSheet` and `RecipeStatesBand`, restyled |
| Footer | info line: "Only approved workflows can start a run. Runs, success rate and average time per workflow are not served yet." | caption |

No new tokens.

## 3. States (all in the prototype, `?state=`)

| State | What shows | Source |
|---|---|---|
| Workflows | the table; Plan opens by default | 200 with recipes |
| Reading | six skeleton rows and "Reading workflows…" | first read |
| No workflows | the box "No workflows are installed" | empty list |
| Filter matches nothing | "No workflows match" | client filter |
| Out of date | the last list with the amber line | `IsStale` |
| Can't read the answer | `Labels.RecipeUnreadable`-style box | decode failure |
| Sheet: approved, not approved, out of date | as section 2; not approved shows the callout and the terminal copy | `standing`, `stale[]` |
| Sheet: no outputs | "This workflow does not produce anything." (`Labels.NoOutputs`) | empty `outputs` |

## 4. Data

Served: `recipes[{id, version, hash, standing, via, stale[], inputs[{key, label, type, kind, generate}], urgency, outputs[], orderedBy, states[{id, order, branchOf}], loops[{name, from, to}], name}]`. The detail route adds the recipe's states with kinds.

Gaps (Annotations on):
- **Plain step names.** State ids (`intake`, `exec-review`) are not display names; the prototype shows them as served. Needs `name` and `why` per step (the same engine request as the Workflows spec).
- **Plain template names.** `name` is the instance-name template, not a title; the prototype's plain names ("Build and prove" for `workflow.phase`) come from a map in the app until recipes carry a title and a one-line description.
- **Usage statistics.** API P12.

## 5. Actions and keys

| Key or click | Does |
|---|---|
| J, K, Enter, Esc, `/` | move, open, close, search |
| Approve new workflows | goes to Settings at the held approve-all (never approves here) |
| Copy chips | the id, `sindri recipe approve <id>` |

No write from this page.

## 6. Copy (`Labels.Recipes.cs`)

Reuse: `InputsHeading` ("What it needs"), `OutputsHeading`, `NoOutputs`, `LoopCapWords`, `RecipeDetails`, `LoadingRecipe`, `RecipeUnreadable`, `RecipeGone`. Add: the standing words, the stale reasons (A step it uses changed since it was approved; A workflow it starts is out of date; It is written in an old format), column headers, the empty and filter boxes, the footer line.

## 7. Tests the ui lane must add or change

- `RecipesPageRowsTests`: plain name map with raw id fallback, standing and stale words, counts.
- `RecipesPageStatesTests`: every state in section 3.
- `RecipeSheetLayoutTests`: callouts per standing, loops in words, no-outputs line.
- Frames at 1440 and 900 for the table with the sheet open.

## 8. Build order (each slice red test first)

1. **Standing and names.** Red: `RecipesPageRowsTests`. Standing and stale words, the name map.
2. **Table.** Red: row and band tests. `RecipesPageViewModel`, the table, filters, search; the placeholder page is replaced.
3. **States.** Red: `RecipesPageStatesTests`.
4. **Sheet layout.** Red: `RecipeSheetLayoutTests`. Restyle the existing sheet, callouts, step chips.
5. **Pending data (separate PRs).** Step names and the template title and description from the engine; usage statistics when P12 lands.

## 9. Open items for the ui lane

1. **Two pages named "Workflows".** The rail's Workflows is runs; this page's title says "Workflows" for templates. Plain words may want "Workflow templates"; the prototype uses "Workflows" with the rail tip "Recipes" until the owner chooses.
2. **Approve here.** Approving one template needs the typed confirmation control from the Setup spec; until then the sheet shows terminal copy.
