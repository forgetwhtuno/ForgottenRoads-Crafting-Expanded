using System;

namespace ErenshorCraftingExpanded
{
    public sealed class NativeConsumableFacts
    {
        public bool GeneralSlot;
        public bool Stackable;
        public bool Disposable;
        public bool MustBeEquippedToClick;
        public bool Unique;
        public bool Template;
        public bool FuelSource;
        public bool Relic;
        public bool RareItem;
        public bool PlayerCannotSell;
        public bool NoTradeNoDestroy;
        public bool SimPlayersCantGet;
        public bool HasIcon;
        public bool HasClickEffect;
        public bool HasTeachSpell;
        public bool HasTeachSkill;
        public bool HasQuestRead;
        public bool HasAura;
        public bool HasWornEffect;
        public bool HasWeaponProc;
        public int ItemValue;
        public int ItemLevel;
        public double ItemSpellCastTime;

        // Current Spell fields proven from the supplied Assembly-CSharp. ItemIcon.UseConsumable
        // passes ItemEffectOnClick plus Item.SpellCastTime to CastSpell.StartSpell; SpellVessel then
        // resolves the same Spell object. These are behavior evidence, not name-based guesses.
        public int SpellTypeCode;
        public int DamageTypeCode;
        public int EffectRequiredLevel;
        public double ManaCost;
        public double Cooldown;
        public double SpellRange;
        public double DirectHp;
        public double FlatMana;
        public double TargetHealing;
        public double CasterHealing;
        public double PercentManaRestoration;
        public double LevelScaledManaRestoration;
        public double TargetDamage;
        public double BleedDamagePercent;
        public bool Lifetap;
        public bool BeneficialType;
        public bool HealType;
        public bool SelfOnly;
        public bool ApplyToCaster;
        public bool InflictOnSelf;
        public bool GroupEffect;
        public bool AreaEffect;
        public bool PetSummon;
        public bool CharmTarget;
        public bool CrowdControl;
        public bool HasStatusEffect;
    }

    public static class NativeConsumablePolicy
    {
        // Spell.SpellType values in the supplied current assembly.
        public const int SpellTypeBeneficial = 2;
        public const int SpellTypeArea = 3;
        public const int SpellTypePointBlankArea = 4;
        public const int SpellTypeMisc = 5;
        public const int SpellTypeHeal = 6;

        // The ItemDB click-consumable records use the Beneficial/Magic-shaped direct-HP signature
        // (Type=2, MyDamageType=1, HP>0). Test it before generic Beneficial utility; otherwise a
        // real health donor is silently classified as Utility. The older Heal shapes remain
        // constrained compatibility signatures.
        public const int ClickHpSpellType = SpellTypeBeneficial;
        public const int ClickHpDamageBranch = 1;
        public const int HealBranchHp = 0;
        public const int HealBranchMana = 1;

        public static bool IsSafeCommodityConsumable(NativeConsumableFacts facts)
        {
            return string.IsNullOrEmpty(RejectionReason(facts));
        }

        public static string RejectionReason(NativeConsumableFacts facts)
        {
            if (facts == null) return "facts unavailable";
            if (!facts.GeneralSlot) return "not General slot";
            if (!facts.Stackable) return "not stackable";
            if (!facts.Disposable) return "not Disposable";
            if (facts.MustBeEquippedToClick) return "equip-to-click";
            if (facts.Unique) return "Unique";
            if (facts.Template) return "Template";
            if (facts.FuelSource) return "FuelSource";
            if (facts.Relic) return "Relic";
            if (facts.RareItem) return "RareItem";
            if (facts.PlayerCannotSell) return "PlayerCannotSell";
            if (facts.NoTradeNoDestroy) return "NoTradeNoDestroy";
            if (facts.SimPlayersCantGet) return "SimPlayersCantGet";
            if (!facts.HasIcon) return "missing icon";
            if (!facts.HasClickEffect) return "missing ItemEffectOnClick";
            if (facts.HasTeachSpell) return "TeachSpell baggage";
            if (facts.HasTeachSkill) return "TeachSkill baggage";
            if (facts.HasQuestRead) return "quest-read baggage";
            if (facts.HasAura) return "Aura baggage";
            if (facts.HasWornEffect) return "WornEffect baggage";
            if (facts.HasWeaponProc) return "WeaponProcOnHit baggage";
            if (facts.ItemValue <= 0) return "non-positive ItemValue";

            // ItemIcon.UseConsumable can decrement Disposable even though StartSpell's bool result is
            // ignored. A safe cloned commodity therefore must not depend on spending mana and must be
            // self-usable without a selected target. This prevents "consumed but native cast failed"
            // donors from being admitted as potions/provisions.
            if (facts.ManaCost > 0.0) return "positive Spell.ManaCost";
            if (!facts.SelfOnly) return "not self-only";
            if (facts.GroupEffect) return "group effect";
            if (facts.AreaEffect) return "AE/PBAE effect";
            if (facts.PetSummon) return "pet/summon effect";
            if (facts.CharmTarget) return "charm effect";
            if (facts.TargetDamage > 0.0) return "target damage";
            if (facts.BleedDamagePercent > 0.0) return "bleed damage";
            if (facts.Lifetap) return "lifetap";
            if (facts.CrowdControl) return "crowd control";
            return string.Empty;
        }

        public static bool IsHealthRecovery(NativeConsumableFacts facts)
        {
            if (facts == null) return false;
            bool directClickHp = facts.SpellTypeCode == ClickHpSpellType && facts.DamageTypeCode == ClickHpDamageBranch && facts.DirectHp > 0.0;
            bool healBranchHp = facts.HealType && facts.SpellTypeCode == SpellTypeHeal && facts.DamageTypeCode == HealBranchHp && facts.DirectHp > 0.0;
            return directClickHp || healBranchHp;
        }

        public static bool IsManaRecovery(NativeConsumableFacts facts)
        {
            if (facts == null) return false;
            return facts.HealType && facts.SpellTypeCode == SpellTypeHeal && facts.DamageTypeCode == HealBranchMana &&
                (facts.FlatMana > 0.0 || facts.PercentManaRestoration > 0.0);
        }

        public static bool IsUtility(NativeConsumableFacts facts)
        {
            if (facts == null || IsHealthRecovery(facts) || IsManaRecovery(facts)) return false;
            if (!facts.SelfOnly) return false;
            return facts.BeneficialType;
        }

        public static bool MatchesFamily(NativeConsumableFacts facts, NativeConsumableFamily family)
        {
            if (!IsSafeCommodityConsumable(facts)) return false;
            bool health = IsHealthRecovery(facts);
            bool mana = IsManaRecovery(facts);
            if (family == NativeConsumableFamily.HealthRecovery) return health;
            if (family == NativeConsumableFamily.ManaRecovery) return mana;
            if (family == NativeConsumableFamily.Utility) return IsUtility(facts);
            return health || mana;
        }

        public static bool IsProgressionCompatible(NativeConsumableFacts facts, int targetLevel)
        {
            if (facts == null || targetLevel < 1) return false;
            // Current native consumables are allowed to carry zero/unset item/effect levels. A
            // positive native progression marker remains authoritative as a hard ceiling.
            if (facts.ItemLevel > 0 && facts.ItemLevel > targetLevel) return false;
            if (facts.EffectRequiredLevel > 0 && facts.EffectRequiredLevel > targetLevel) return false;
            return true;
        }

        public static double PowerMagnitude(NativeConsumableFacts facts, NativeConsumableFamily family)
        {
            if (facts == null) return 0.0;
            if (family == NativeConsumableFamily.HealthRecovery) return facts.DirectHp;
            if (family == NativeConsumableFamily.ManaRecovery)
            {
                // Flat mana is preferred by donor selection when present because it stays bounded as
                // max mana grows. Percent mana is still a proven native family and remains eligible
                // when no flat-mana donor exists.
                return facts.FlatMana > 0.0 ? facts.FlatMana : facts.PercentManaRestoration;
            }
            return facts.ItemValue;
        }

        public static bool UsesPercentageMana(NativeConsumableFacts facts)
        {
            return facts != null && IsManaRecovery(facts) && facts.FlatMana <= 0.0 && facts.PercentManaRestoration > 0.0;
        }

        public static string MagnitudeSource(NativeConsumableFacts facts, NativeConsumableFamily family)
        {
            if (facts == null) return "unknown";
            if (family == NativeConsumableFamily.HealthRecovery) return "Spell.HP";
            if (family == NativeConsumableFamily.ManaRecovery)
                return facts.FlatMana > 0.0 ? "Spell.Mana" : (facts.PercentManaRestoration > 0.0 ? "Spell.PercentManaRestoration" : "unknown");
            if (family == NativeConsumableFamily.Utility) return facts.HasStatusEffect ? "Spell.StatusEffectToApply" : "Spell beneficial/self semantics";
            return "family-specific";
        }

        public static string RecoveryFamilyProof(NativeConsumableFacts facts)
        {
            if (facts == null) return "facts unavailable";
            if (IsHealthRecovery(facts))
            {
                if (facts.SpellTypeCode == ClickHpSpellType && facts.DamageTypeCode == ClickHpDamageBranch)
                    return "ItemDB direct HP signature (Spell.Type=2, MyDamageType=1, Spell.HP)";
                return "native Heal physical branch (Spell.HP)";
            }
            if (IsManaRecovery(facts)) return "native Heal magic branch (Spell.Mana/PercentManaRestoration)";
            if (facts.BeneficialType) return "generic Beneficial utility (no recovery signature)";
            return "no supported native recovery signature";
        }

        public static bool IsHarmful(NativeConsumableFacts facts)
        {
            return facts != null && (facts.TargetDamage > 0.0 || facts.BleedDamagePercent > 0.0 || facts.Lifetap || facts.CrowdControl);
        }

        public static bool HasUnsafeWorldShape(NativeConsumableFacts facts)
        {
            return facts != null && (facts.GroupEffect || facts.AreaEffect || facts.PetSummon || facts.CharmTarget);
        }

        internal static string RunSelfTests()
        {
            NativeConsumableFacts health = SafeFacts(); health.SpellTypeCode = ClickHpSpellType; health.BeneficialType = true; health.DamageTypeCode = ClickHpDamageBranch; health.DirectHp = 25;
            if (!MatchesFamily(health, NativeConsumableFamily.HealthRecovery) || !MatchesFamily(health, NativeConsumableFamily.AnyRecovery) || MatchesFamily(health, NativeConsumableFamily.ManaRecovery))
                return "FAIL current ItemDB direct HP consumable family";
            if (MatchesFamily(health, NativeConsumableFamily.Utility) || RecoveryFamilyProof(health).IndexOf("Spell.HP", StringComparison.Ordinal) < 0)
                return "FAIL direct HP signature lost recovery precedence/proof";
            NativeConsumableFacts legacyHealth = SafeFacts(); legacyHealth.SpellTypeCode = SpellTypeHeal; legacyHealth.HealType = true; legacyHealth.DamageTypeCode = HealBranchHp; legacyHealth.DirectHp = 15;
            if (!MatchesFamily(legacyHealth, NativeConsumableFamily.HealthRecovery)) return "FAIL native Heal direct HP compatibility";
            NativeConsumableFacts legacyHealShape = SafeFacts(); legacyHealShape.TargetHealing = 99;
            if (MatchesFamily(legacyHealShape, NativeConsumableFamily.HealthRecovery)) return "FAIL TargetHealing-only fake donor admitted";

            NativeConsumableFacts mana = SafeFacts(); mana.SpellTypeCode = SpellTypeHeal; mana.HealType = true; mana.DamageTypeCode = HealBranchMana; mana.FlatMana = 20;
            if (!MatchesFamily(mana, NativeConsumableFamily.ManaRecovery) || !MatchesFamily(mana, NativeConsumableFamily.AnyRecovery)) return "FAIL flat mana consumable family";
            NativeConsumableFacts percentMana = SafeFacts(); percentMana.SpellTypeCode = SpellTypeHeal; percentMana.HealType = true; percentMana.DamageTypeCode = HealBranchMana; percentMana.PercentManaRestoration = 10;
            if (!MatchesFamily(percentMana, NativeConsumableFamily.ManaRecovery) || !UsesPercentageMana(percentMana)) return "FAIL percent mana consumable family";

            NativeConsumableFacts utility = SafeFacts(); utility.SpellTypeCode = SpellTypeBeneficial; utility.BeneficialType = true; utility.HasStatusEffect = true;
            if (!MatchesFamily(utility, NativeConsumableFamily.Utility) || MatchesFamily(utility, NativeConsumableFamily.AnyRecovery)) return "FAIL utility consumable family";
            NativeConsumableFacts unsafeUtility = SafeFacts(); unsafeUtility.SelfOnly = false; unsafeUtility.BeneficialType = true;
            if (MatchesFamily(unsafeUtility, NativeConsumableFamily.Utility)) return "FAIL target-dependent utility donor admitted";

            NativeConsumableFacts harmful = SafeFacts(); harmful.SpellTypeCode = SpellTypeHeal; harmful.HealType = true; harmful.DamageTypeCode = HealBranchHp; harmful.DirectHp = 5; harmful.TargetDamage = 1;
            if (MatchesFamily(harmful, NativeConsumableFamily.AnyRecovery)) return "FAIL harmful mixed-effect donor admitted";
            NativeConsumableFacts fake = SafeFacts(); fake.Disposable = false; fake.SpellTypeCode = SpellTypeHeal; fake.HealType = true; fake.DamageTypeCode = HealBranchHp; fake.DirectHp = 99;
            if (MatchesFamily(fake, NativeConsumableFamily.AnyRecovery)) return "FAIL non-disposable fake recovery admitted";
            NativeConsumableFacts disposableOnly = SafeFacts(); disposableOnly.HasClickEffect = false;
            if (IsSafeCommodityConsumable(disposableOnly)) return "FAIL Disposable-like arbitrary item admitted";
            NativeConsumableFacts costsMana = SafeFacts(); costsMana.ManaCost = 1;
            if (IsSafeCommodityConsumable(costsMana)) return "FAIL mana-cost click item admitted";
            NativeConsumableFacts area = SafeFacts(); area.GroupEffect = true;
            if (IsSafeCommodityConsumable(area)) return "FAIL group/world-shape donor admitted";
            NativeConsumableFacts protectedItem = SafeFacts(); protectedItem.NoTradeNoDestroy = true;
            if (IsSafeCommodityConsumable(protectedItem)) return "FAIL protected donor admitted";

            NativeConsumableFacts levelZero = SafeFacts(); levelZero.ItemLevel = 0; levelZero.EffectRequiredLevel = 0;
            if (!IsSafeCommodityConsumable(levelZero) || !IsProgressionCompatible(levelZero, 6)) return "FAIL zero native levels should not define consumable identity";
            NativeConsumableFacts tooHighItem = SafeFacts(); tooHighItem.ItemLevel = 20;
            if (IsProgressionCompatible(tooHighItem, 6)) return "FAIL high item-level donor admitted early";
            NativeConsumableFacts tooHighEffect = SafeFacts(); tooHighEffect.EffectRequiredLevel = 20;
            if (IsProgressionCompatible(tooHighEffect, 6)) return "FAIL high effect-level donor admitted early";
            return "PASS native consumable policy";
        }

        private static NativeConsumableFacts SafeFacts()
        {
            NativeConsumableFacts f = new NativeConsumableFacts();
            f.GeneralSlot = true; f.Stackable = true; f.Disposable = true; f.MustBeEquippedToClick = false;
            f.Unique = false; f.Template = false; f.FuelSource = false; f.Relic = false; f.RareItem = false;
            f.PlayerCannotSell = false; f.NoTradeNoDestroy = false; f.SimPlayersCantGet = false;
            f.HasIcon = true; f.HasClickEffect = true; f.ItemValue = 1; f.ItemLevel = 1; f.SelfOnly = true;
            return f;
        }
    }

    public static class ConsumableEconomyPolicy
    {
        public const int NativeMaximumGeneralOutputQuantity = 5;

        public static bool HasNoObviousVendorValueInversion(int outputUnitValue, int inputTotalValue)
        {
            if (outputUnitValue <= 0 || inputTotalValue <= 0) return false;
            return outputUnitValue * NativeMaximumGeneralOutputQuantity <= inputTotalValue;
        }

        internal static string RunSelfTests()
        {
            if (!HasNoObviousVendorValueInversion(1, 5)) return "FAIL exact safe value boundary";
            if (HasNoObviousVendorValueInversion(2, 9)) return "FAIL output quantity inversion admitted";
            if (!HasNoObviousVendorValueInversion(2, 10)) return "FAIL valid tier-two output rejected";
            return "PASS consumable economy policy";
        }
    }
}
