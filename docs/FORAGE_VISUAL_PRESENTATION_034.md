# Crafting Expanded 0.3.4 forage visual presentation hardening

The prior auto gate accepted mesh/bounds alone, allowing materialless or displaced clone geometry
to receive interaction presentation. Clone branches without usable materials are now pruned; final
visuals must have active renderers, finite nontrivial bounds, sane scale, coherent renderer anchor,
and auto-node grounding proof. Label/collider/highlight bind only to audited bounds.

The world label and red bar are larger with hard black UI outlines. Each auto attempt records
`forage_visual_spawn`; first family success/failure is bounded. Portable tests, source guards, and
current-assembly build/install passed.
