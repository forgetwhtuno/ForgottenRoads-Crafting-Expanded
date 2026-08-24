# Crafting Expanded 0.3.5 forage population balance

This follow-up changes only safe forage population density and Starter-area mix selection.

The auto-placement target is now up to five clusters instead of three. This is an upper target,
not a forced count: the existing NavMesh path, wall-anchor, ground-slope, obstacle, actor-clearance,
interaction-space, and minimum-separation checks remain authoritative. Scenes with fewer safe points
still spawn fewer nodes.

For open Starter ecology, deterministic floors select these proven common families when available:

1. Wild Herb
2. Resin Sprig
3. Field Fiber

Remaining safe points use the existing deterministic density-weighted selection and per-resource caps.
Unavailable items, visuals, world-band rules, regional rules, and evidence gates continue to remove a
candidate before selection. Player foraging skill remains separate from world existence.

No visual construction, label, highlight, collider, interaction, gather, reward, XP, depletion,
or respawn behavior is changed by this pass.
