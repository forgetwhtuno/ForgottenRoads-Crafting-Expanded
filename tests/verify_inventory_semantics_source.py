#!/usr/bin/env python3
from pathlib import Path
import re
import subprocess
import sys

REPO = Path(__file__).resolve().parents[1]
ROOT = REPO.parents[1]
ASSEMBLY = ROOT / 'CURRENT_GAME_REFERENCES' / 'Assembly-CSharp.dll'

def fail(msg):
    print('FAIL:', msg)
    sys.exit(1)

def read(rel):
    return (REPO / rel).read_text(encoding='utf-8-sig')

required_paths = [
    'src/Items/ItemSemanticsPolicy.cs',
    'src/Compatibility/GameItemRegistryApi.cs',
    'src/Items/ExpandedContentItems.cs',
    'src/Crafting/ExpandedContentRecipes.cs',
    'src/Crafting/RecipeTemplateItemPolicy.cs',
]
for rel in required_paths:
    if not (REPO / rel).is_file(): fail('missing ' + rel)

plugin = read('src/ErenshorCraftingExpandedPlugin.cs')
if '"0.3.5"' not in plugin: fail('expected source version 0.3.5 not found')

if not ASSEMBLY.is_file(): fail('current Assembly-CSharp.dll missing')
try:
    strings = subprocess.check_output(['strings', str(ASSEMBLY)], text=True, errors='replace')
except FileNotFoundError:
    # Windows reconciliation workspaces may not ship GNU strings. Managed assembly
    # metadata is ASCII/UTF-8-compatible for these symbol probes, so byte scanning
    # is equivalent and avoids borrowing a tool from another environment.
    strings = ASSEMBLY.read_bytes().decode('latin1', errors='ignore')
except Exception as ex:
    fail('could not inspect current assembly symbols: ' + type(ex).__name__)
for symbol in ['PlayerCannotSell','NoTradeNoDestroy','ItemValue','Stackable','Disposable',
               'AssignQuestOnRead','CompleteOnRead','SimPlayersCantGet','VendorWindow','QuickSell','DoSellStack',
               'BuyBack','TrashSlot','OpenConfirmDelete','SubmitDelete']:
    if symbol not in strings: fail('current assembly symbol missing: ' + symbol)

policy = read('src/Items/ItemSemanticsPolicy.cs')
for token in ['RawResource','ProcessedComponent','FinishedEquipment',
              'PlayerCannotSell = false','NoTradeNoDestroy = false','SimPlayersCantGet = false','Stackable',
              'IsConservativeProcessedValue','outputValue == totalInputValue']:
    if token not in policy: fail('policy contract missing: ' + token)

registry = read('src/Compatibility/GameItemRegistryApi.cs')
for token in ['ApplyOrdinaryItemSemantics','ItemSemanticsPolicy.ForRawResource',
              'ItemSemanticsPolicy.ForProcessedComponent','ItemSemanticsPolicy.ForFinishedEquipment',
              'SetField(item, "AssignQuestOnRead", null)','SetField(item, "CompleteOnRead", null)',
              'UnityEngine.Object.Instantiate(source)','BuildOwnedInventorySemanticsLines',
              'BuildNativeInventorySemanticsReferenceLines','simBlocked=']:
    if token not in registry: fail('registry semantics contract missing: ' + token)
if registry.count('ApplyOrdinaryItemSemantics(existing') < 2:
    fail('save/reload/re-registration semantic normalization is not present for both item paths')
if registry.count('SetField(clone, "Id", definition.Id)') < 2:
    fail('custom clone stable identity override missing; donor/custom stacks could collide')
if 'ApplyOrdinaryItemSemantics(donor' in registry or 'SetField(donor,' in registry:
    fail('native donor mutation detected')
# The ordinary clone paths must not hard-code the old inventory lock.
for method, end_marker in [('CloneExpandedItem', 'FindSafeBaseItem'), ('CloneAndConfigure', 'ClearClassRestrictions')]:
    start = registry.find('private static object ' + method)
    end = registry.find('private static ', start + 10)
    if start < 0 or end < 0: fail('could not isolate ' + method)
    block = registry[start:end]
    if re.search(r'SetField\([^\n]*"PlayerCannotSell"\s*,\s*true', block): fail(method + ' still forces PlayerCannotSell')
    if re.search(r'SetField\([^\n]*"NoTradeNoDestroy"\s*,\s*true', block): fail(method + ' still forces NoTradeNoDestroy')
# Recipe templates remain intentionally protected.
template_policy = read('src/Crafting/RecipeTemplateItemPolicy.cs')
for token in ['PlayerCannotSell = true','NoTradeNoDestroy = true','SafeVendorValue = 0']:
    if token not in template_policy: fail('recipe-template safety regressed: ' + token)

# Crafting Expanded must not simulate merchant or trash mutations itself.
all_source = '\n'.join(p.read_text(encoding='utf-8-sig') for p in (REPO/'src').rglob('*.cs'))
for forbidden in ['[HarmonyPatch(typeof(VendorWindow)', '[HarmonyPatch(typeof(TrashSlot)',
                  'ConfirmDestroyWindow', 'SubmitDelete(', 'Gold +=', 'RemoveItemFromInv(', 'RemoveStackFromInv(']:
    if forbidden in all_source: fail('custom merchant/delete authority introduced: ' + forbidden)

# Stable IDs and Barkguard/Ghostcap/Bloomwood semantic families remain intact.
ids = read('src/Items/CraftingExpandedItemIds.cs')
for token in ['BarkguardBucklerId','GhostcapShivId','BloomwoodStaffId']:
    if token not in ids: fail('stable item identity missing: ' + token)
items = read('src/Items/ExpandedContentItems.cs')
for token in ['"Barkguard Maul"','"Primary", "TwoHandMelee"','"Ghostcap Shiv"',
              '"PrimaryOrSecondary", "OneHandDagger"','"Bloomwood Staff"','"Primary", "TwoHandStaff"']:
    if token not in items: fail('equipment donor-family contract missing: ' + token)

# Processed values are a no-markup intrinsic-value chain.
expected = {
    'Woven Fiber Cord':2, 'Herbal Binder':3, 'Resin-Sealed Grip':4, 'Bloom Tincture':3,
    'Cave Paste':3, 'Starleaf Infusion':7, 'Root Temper':4, 'Ghostcap Extract':7,
}
for name,value in expected.items():
    pat = re.compile(r'Material\([^\n]*"' + re.escape(name) + r'"[^\n]*"component"[^\n]*,\s*' + str(value) + r',')
    if not pat.search(items): fail('processed component intrinsic value mismatch: ' + name)
recipes = read('src/Crafting/ExpandedContentRecipes.cs')
if 'ItemSemanticsPolicy.IsConservativeProcessedValue' not in recipes:
    fail('processed-component per-unit intrinsic-value guard missing')

# Previous live-runtime forage repair must remain present.
controller = read('src/Foraging/ForageNodeController.cs')
for token in ['GrantPending','UpdateTargetedNode','ForageActiveGatherClickPolicy','CancelActiveGather']:
    if token not in controller: fail('foraging runtime repair token missing: ' + token)
if 'Press G to gather' in all_source: fail('old G-key gathering prompt returned')

print('PASS inventory semantics source/current-assembly contract')
