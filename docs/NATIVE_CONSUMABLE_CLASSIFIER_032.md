# Crafting 0.3.2 native consumable classifier recovery

The classifier now gives the observed self-only ItemDB direct-health signature precedence over
generic Beneficial utility: `Spell.Type=2`, `MyDamageType=1`, and positive `Spell.HP`. The native
Heal/Physical health and Heal/Magic mana signatures remain supported as constrained compatible
families.

The bounded consumable diagnostic now prints the classification proof with the existing donor
safety fields. Donor effects are still reused exactly; the repair does not synthesize spell effects
or write player health/mana directly.

Live QA: use `/craftdiag consumables native`, verify health candidates are labeled
`HealthRecovery` with the direct-HP proof, craft each health output, and confirm one native click
consumes one item and produces the donor's native effect.
