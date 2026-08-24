#!/usr/bin/env python3
"""Exact current-assembly/source regression gate for Crafting 0.3.2 consumable forensics.

This intentionally does not claim live Unity ItemDB execution. It pins the supplied Assembly-CSharp
method bodies that establish item-use entry, StartSpell overload, effect resolution, HP/mana mutation,
and ItemDatabase resource loading, then verifies the runtime policy consumes those proven fields.
"""
from pathlib import Path
import hashlib
import struct

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT.parents[1]
ASSEMBLY = PROJECT / "CURRENT_GAME_REFERENCES" / "Assembly-CSharp.dll"

EXPECTED_ASSEMBLY = "B840CB8076ED0553F7DC3BEB4042ABA653917882F763181EC0D2C13C26C17847"
METHODS = {
    "ItemIcon.UseConsumable": (0x2F520, 630, "785CF3A1049B45AC4A225CE48D653E783FFC6F02B3D984C1EAE22003B46DD44E"),
    "CastSpell.StartSpell(Spell,Stats,float)": (0x1A7C8, 799, "D8A6EEBADA35D6289AD193CDA7A1A03D76FACD7790F11572735E89CDA874A119"),
    "SpellVessel.ResolveSpell": (0x428C8, 25628, "043871E1928439B37065CB9FA933B24D4FFA5C562ED0DFEB41872D517F5C15FC"),
    "Stats.HealMe(effect-aware)": (0x78CE0, 524, "78C39ED98C62439C58F3D73FFFA162A0F3CC5674E1DFBA7E37100CEE434A3D8A"),
    "ItemDatabase.Start": (0x4F96C, 234, "F1445BEAC7EEE02DA041BE97F5FBBFD7E25191FE55161FB73BE7FFC90A53D5E9"),
}


def req(cond, msg):
    if not cond:
        raise AssertionError(msg)


def section_map(raw):
    pe = struct.unpack_from("<I", raw, 0x3C)[0]
    req(raw[pe:pe + 4] == b"PE\0\0", "not a PE image")
    coff = pe + 4
    count = struct.unpack_from("<H", raw, coff + 2)[0]
    opt_size = struct.unpack_from("<H", raw, coff + 16)[0]
    off = coff + 20 + opt_size
    sections = []
    for _ in range(count):
        virtual_size, virtual_address, raw_size, raw_ptr = struct.unpack_from("<IIII", raw, off + 8)
        sections.append((virtual_address, max(virtual_size, raw_size), raw_ptr))
        off += 40
    return sections


def rva_offset(sections, rva):
    for va, size, ptr in sections:
        if va <= rva < va + size:
            return ptr + (rva - va)
    raise AssertionError("RVA not mapped: 0x%X" % rva)


def method_code(raw, sections, rva):
    off = rva_offset(sections, rva)
    first = raw[off]
    if first & 3 == 2:
        size = first >> 2
        return raw[off + 1:off + 1 + size]
    req(first & 3 == 3, "unknown method header at RVA 0x%X" % rva)
    flags_size = struct.unpack_from("<H", raw, off)[0]
    header = (flags_size >> 12) * 4
    size = struct.unpack_from("<I", raw, off + 4)[0]
    return raw[off + header:off + header + size]


def text(rel):
    return (ROOT / rel).read_text(encoding="utf-8-sig")


def main():
    raw = ASSEMBLY.read_bytes()
    req(hashlib.sha256(raw).hexdigest().upper() == EXPECTED_ASSEMBLY, "unexpected current Assembly-CSharp")
    sections = section_map(raw)
    for label, (rva, size, digest) in METHODS.items():
        code = method_code(raw, sections, rva)
        req(len(code) == size, "%s method size changed" % label)
        req(hashlib.sha256(code).hexdigest().upper() == digest, "%s IL changed" % label)

    # Exact field/member names used by the pinned methods must still be present in the supplied image.
    for token in (
        b"UseConsumable", b"ItemEffectOnClick", b"SpellCastTime", b"Disposable", b"StartSpell",
        b"ResolveSpell", b"HealMe", b"CurrentHP", b"CurrentMana", b"PercentManaRestoration",
        b"StatusEffectToApply", b"RequiredLevel", b"ManaCost", b"Cooldown", b"SelfOnly",
    ):
        req(token in raw, "assembly member missing: %s" % token.decode())

    policy = text("src/Items/NativeConsumablePolicy.cs")
    registry = text("src/Compatibility/GameItemRegistryApi.cs")
    plugin = text("src/ErenshorCraftingExpandedPlugin.cs")
    items = text("src/Items/ExpandedContentItems.cs")

    for token in (
        "DirectHp", "FlatMana", "SpellTypeCode", "DamageTypeCode", "EffectRequiredLevel",
        "ManaCost", "ItemSpellCastTime", "IsProgressionCompatible", "PowerMagnitude",
        "Spell.HP", "Spell.Mana", "Spell.PercentManaRestoration", "ClickHpSpellType",
        "ClickHpDamageBranch", "RecoveryFamilyProof", "directClickHp",
    ):
        req(token in policy, "policy evidence missing: " + token)
    req("facts.ItemLevel > 0 && facts.ItemLevel > targetLevel" in policy, "positive item-level ceiling missing")
    req("facts.EffectRequiredLevel > 0 && facts.EffectRequiredLevel > targetLevel" in policy, "effect-level ceiling missing")
    req("facts.ItemLevel > 0;" not in policy, "nonzero ItemLevel still defines consumable identity")
    req("facts.ManaCost > 0.0" in policy and "!facts.SelfOnly" in policy, "consume-on-failed-cast safety gates missing")
    req("facts.SpellTypeCode == ClickHpSpellType" in policy and "facts.DamageTypeCode == ClickHpDamageBranch" in policy,
        "current ItemDB direct-HP signature missing")
    req("IsHealthRecovery(facts) || IsManaRecovery(facts)" in policy,
        "recovery precedence is not evaluated before generic utility")

    for token in (
        'SetField(item, "ItemEffectOnClick", donorEffect)', 'SetField(item, "ItemIcon", donorIcon)',
        'SetField(item, "SpellCastTime", donorCastTime)', "BuildNativeConsumableReferenceLines",
        "native Disposable quantity-1", "progressionRejected", "rank=",
    ):
        req(token in registry, "runtime consumable contract missing: " + token)
    req("ClearClassRestrictions(item);" not in registry[registry.index("private static bool ApplyConsumableSemantics"):registry.index("private static object FindSafeEquipmentDonor")],
        "consumable clone rewrites native class eligibility")
    req('"/craftdiag consumables native"' in plugin, "native developer diagnostic command missing")
    req("RejectionReason" in policy, "bounded native donor rejection-reason policy missing")
    req("reject={" in registry and "safe donors first" in registry and "RecoveryFamilyProof" in registry,
        "native donor diagnostic does not surface bounded rejection reasons/proof/safe-first rows")
    for name in ("Field Tonic", "Wayfarer Tonic", "Starleaf Tonic"):
        pos = items.index(name)
        req("HealthRecovery" in items[pos:pos + 420], name + " is not pinned to direct health family")

    print("PASS native consumable forensics gate")
    print("assembly=" + EXPECTED_ASSEMBLY)
    print("methodBodies=%d sourceContracts=PASS" % len(METHODS))


if __name__ == "__main__":
    main()
