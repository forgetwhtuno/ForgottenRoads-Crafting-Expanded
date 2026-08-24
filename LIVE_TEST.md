# Crafting Expanded 0.3.5 forage population live QA

1. Enter a safe open Starter-area scene and run the existing forage diagnostic.
2. Confirm accepted geometry still reports only safe wall/ground/NavMesh points and can reach up
   to five clusters when five safe points survive; do not expect five if safety gates reject points.
3. Confirm the first eligible open-area mix is representative: Wild Herb, Resin Sprig, and Field
   Fiber should each appear once when their item, visual, environment, and world evidence are valid.
4. Confirm remaining safe points use deterministic weighted selection and respect resource caps.
5. Gather one node and verify the existing exact-one reward, XP, depletion, and respawn behavior.
6. Verify visuals remain grounded and fail-closed. This pass intentionally does not alter visual
   construction or presentation code.

Verify Resin Sprig and Wild Herb show visible grounded meshes and a larger outlined name/bar tracks
the real mesh. Both must remain RMB gatherable. Confirm no label-only or collider-only node is
admitted. `forage_visual_spawn` must show real donor/audit details and `bindingAllowed=True` only
for accepted visible visuals.
