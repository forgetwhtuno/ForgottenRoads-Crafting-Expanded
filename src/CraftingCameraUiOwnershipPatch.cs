using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ErenshorCraftingExpanded
{
    // Crafting-owned camera containment. Installation is manual and fail-closed: the current
    // CameraController IL shape is re-proved before Harmony is allowed to touch UsingUI().
    // The postfixes are monotonic and can only promote false -> true for an active
    // Crafting-owned retained-UI or captured-forage pointer gesture.
    internal static class CraftingCameraUiOwnershipPatch
    {
        private static bool _installed;
        private static float _lastNativeUsingUiTrueAt = -999f;

        internal static bool IsInstalled { get { return _installed; } }

        internal static bool NativeUiRecentlyActive(float now)
        {
            return now <= _lastNativeUsingUiTrueAt + 0.20f;
        }

        internal static void ResetRuntimeState()
        {
            _installed = false;
            _lastNativeUsingUiTrueAt = -999f;
        }

        internal static bool TryInstall(Harmony harmony, out string diagnostic)
        {
            ResetRuntimeState();
            diagnostic = "not-checked";
            if (harmony == null)
            {
                diagnostic = "Harmony unavailable";
                return false;
            }

            MethodInfo usingUi;
            MethodInfo mouseLook;
            string proof;
            if (!CraftingCameraUiCompatibility.TryVerify(out usingUi, out mouseLook, out proof))
            {
                diagnostic = proof;
                return false;
            }

            try
            {
                MethodInfo usingUiPostfix = typeof(CraftingCameraUiOwnershipPatch).GetMethod(
                    "UsingUiPostfix", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo mouseLookPrefix = typeof(CraftingCameraUiOwnershipPatch).GetMethod(
                    "MouseLookPrefix", BindingFlags.Static | BindingFlags.NonPublic);
                if (usingUiPostfix == null || mouseLookPrefix == null)
                {
                    diagnostic = "ownership patch method unavailable";
                    return false;
                }

                // Keep the capture narrow. DraggingUIElement + UsingUI contain CameraController
                // only for the initiating forage RMB press, while PlayerControl.MouseLook is
                // skipped during that short ownership window. We intentionally do NOT globally
                // force EventSystem pointer ownership because that same gate also protects
                // LandMovement/WaterMovement; once the selecting RMB is released the channel
                // continues without camera/input ownership so real movement can interrupt it.
                harmony.Patch(usingUi, null, new HarmonyMethod(usingUiPostfix));
                harmony.Patch(mouseLook, new HarmonyMethod(mouseLookPrefix), null);
                _installed = true;
                diagnostic = proof;
                return true;
            }
            catch (Exception ex)
            {
                _installed = false;
                diagnostic = "patch failed: " + ex.GetType().Name;
                return false;
            }
        }

        private static void UsingUiPostfix(ref bool __result)
        {
            // Preserve the raw native answer before monotonic promotion. This lets a captured
            // forage interaction detect a real open native UI without detecting its own claim.
            bool nativeUsingUi = __result;
            if (nativeUsingUi)
            {
                try { _lastNativeUsingUiTrueAt = Time.unscaledTime; } catch { }
            }
            __result = CraftingCameraUiPolicy.PromoteUsingUi(nativeUsingUi, CraftingUiPointerOwnership.HasOwners);
        }

        private static bool MouseLookPrefix()
        {
            return !ForageNodeController.OwnsCapturedPointerInput;
        }
    }

    internal static class CraftingCameraUiCompatibility
    {
        private static readonly Dictionary<short, OpCode> OpCodesByValue = BuildOpCodeTable();

        internal static bool TryVerify(out MethodInfo usingUi, out MethodInfo mouseLook, out string diagnostic)
        {
            usingUi = null;
            mouseLook = null;
            diagnostic = "camera/input containment compatibility not verified";
            try
            {
                Type cameraType = typeof(CameraController);
                BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

                usingUi = cameraType.GetMethod("UsingUI", instanceFlags, null, Type.EmptyTypes, null);
                if (!ExactMethod(usingUi, cameraType, typeof(bool))) return Fail(out usingUi, out mouseLook, out diagnostic, "UsingUI shape mismatch");

                FieldInfo uiWindows = cameraType.GetField("UIWindows", instanceFlags);
                if (uiWindows == null || uiWindows.DeclaringType != cameraType || uiWindows.FieldType != typeof(List<GameObject>))
                    return Fail(out usingUi, out mouseLook, out diagnostic, "UIWindows shape mismatch");

                MethodInfo activeSelf = typeof(GameObject).GetProperty("activeSelf", BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
                if (activeSelf == null || !References(usingUi, uiWindows) || !References(usingUi, activeSelf))
                    return Fail(out usingUi, out mouseLook, out diagnostic, "UsingUI no longer scans UIWindows.activeSelf");

                MethodInfo update = cameraType.GetMethod("Update", instanceFlags, null, Type.EmptyTypes, null);
                MethodInfo modern = cameraType.GetMethod("ModernControls", instanceFlags, null, Type.EmptyTypes, null);
                MethodInfo controls = cameraType.GetMethod("Controls", instanceFlags, null, Type.EmptyTypes, null);
                if (!ExactMethod(update, cameraType, typeof(void)) ||
                    !ExactMethod(modern, cameraType, typeof(void)) ||
                    !ExactMethod(controls, cameraType, typeof(void)))
                    return Fail(out usingUi, out mouseLook, out diagnostic, "camera control method shape mismatch");

                if (!References(update, modern))
                    return Fail(out usingUi, out mouseLook, out diagnostic, "Update no longer references ModernControls");
                if (!References(modern, usingUi))
                    return Fail(out usingUi, out mouseLook, out diagnostic, "ModernControls no longer references UsingUI");

                FieldInfo releaseMouse = cameraType.GetField("releaseMouse", instanceFlags);
                if (releaseMouse == null || releaseMouse.DeclaringType != cameraType || releaseMouse.FieldType != typeof(bool) || !References(modern, releaseMouse))
                    return Fail(out usingUi, out mouseLook, out diagnostic, "ModernControls releaseMouse boundary mismatch");

                MethodInfo getAxis = typeof(Input).GetMethod("GetAxis", staticFlags, null, new Type[] { typeof(string) }, null);
                if (getAxis == null || !References(modern, getAxis))
                    return Fail(out usingUi, out mouseLook, out diagnostic, "ModernControls mouse-axis boundary mismatch");

                FieldInfo dragging = typeof(GameData).GetField("DraggingUIElement", staticFlags);
                if (dragging == null || dragging.FieldType != typeof(bool) || !References(controls, dragging))
                    return Fail(out usingUi, out mouseLook, out diagnostic, "standard Controls drag boundary mismatch");

                MethodInfo pointerOverUi = typeof(EventSystem).GetMethod(
                    "IsPointerOverGameObject", instanceFlags, null, Type.EmptyTypes, null);
                if (!ExactMethod(pointerOverUi, typeof(EventSystem), typeof(bool)))
                    return Fail(out usingUi, out mouseLook, out diagnostic, "EventSystem.IsPointerOverGameObject shape mismatch");

                Type playerControlType = typeof(PlayerControl);
                MethodInfo leftClick = UniqueNamedVoidMethod(playerControlType, "LeftClick", instanceFlags);
                MethodInfo rightClick = UniqueNamedVoidMethod(playerControlType, "RightClick", instanceFlags);
                mouseLook = UniqueNamedVoidMethod(playerControlType, "MouseLook", instanceFlags);
                if (leftClick == null || rightClick == null || mouseLook == null)
                    return Fail(out usingUi, out mouseLook, out diagnostic, "PlayerControl pointer method shape/uniqueness mismatch");
                if (!References(leftClick, pointerOverUi) ||
                    !References(rightClick, pointerOverUi) ||
                    !References(mouseLook, pointerOverUi))
                    return Fail(out usingUi, out mouseLook, out diagnostic, "PlayerControl pointer UI gate relationship changed");
                if (!References(update, pointerOverUi) ||
                    !References(controls, pointerOverUi) ||
                    !References(modern, pointerOverUi))
                    return Fail(out usingUi, out mouseLook, out diagnostic, "CameraController pointer UI gate relationship changed");

                diagnostic = "verified CameraController.UsingUI/UIWindows, EventSystem pointer gate, PlayerControl LeftClick/RightClick/MouseLook, and modern/standard camera boundaries; global EventSystem gate is not patched";
                return true;
            }
            catch (Exception ex)
            {
                return Fail(out usingUi, out mouseLook, out diagnostic, "verification exception: " + ex.GetType().Name);
            }
        }

        private static bool ExactMethod(MethodInfo method, Type declaringType, Type returnType)
        {
            return method != null && method.DeclaringType == declaringType && method.ReturnType == returnType && method.GetParameters().Length == 0;
        }

        private static MethodInfo UniqueNamedVoidMethod(Type type, string name, BindingFlags flags)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;
            MethodInfo found = null;
            MethodInfo[] methods = type.GetMethods(flags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method == null || method.DeclaringType != type || method.ReturnType != typeof(void) || method.Name != name) continue;
                if (found != null) return null;
                found = method;
            }
            return found;
        }

        private static bool Fail(out MethodInfo usingUi, out MethodInfo mouseLook, out string diagnostic, string reason)
        {
            usingUi = null;
            mouseLook = null;
            diagnostic = reason;
            return false;
        }

        private static bool References(MethodBase source, MemberInfo target)
        {
            if (source == null || target == null) return false;
            MethodBody body = source.GetMethodBody();
            if (body == null) return false;
            byte[] il = body.GetILAsByteArray();
            if (il == null || il.Length == 0) return false;
            Type[] typeArgs = source.DeclaringType != null && source.DeclaringType.IsGenericType
                ? source.DeclaringType.GetGenericArguments()
                : Type.EmptyTypes;
            Type[] methodArgs = source.IsGenericMethod ? source.GetGenericArguments() : Type.EmptyTypes;

            int offset = 0;
            while (offset < il.Length)
            {
                OpCode op;
                byte first = il[offset++];
                short key;
                if (first == 0xFE)
                {
                    if (offset >= il.Length) return false;
                    key = unchecked((short)(0xFE00 | il[offset++]));
                }
                else key = first;
                if (!OpCodesByValue.TryGetValue(key, out op)) return false;

                int tokenOffset = -1;
                int operandSize;
                if (!TryGetOperandSize(op.OperandType, il, offset, out operandSize)) return false;
                if (op.OperandType == OperandType.InlineField || op.OperandType == OperandType.InlineMethod ||
                    op.OperandType == OperandType.InlineTok || op.OperandType == OperandType.InlineType)
                    tokenOffset = offset;

                if (tokenOffset >= 0)
                {
                    try
                    {
                        int token = BitConverter.ToInt32(il, tokenOffset);
                        MemberInfo referenced = source.Module.ResolveMember(token, typeArgs, methodArgs);
                        if (SameMember(referenced, target)) return true;
                    }
                    catch { }
                }
                offset += operandSize;
            }
            return false;
        }

        private static bool SameMember(MemberInfo left, MemberInfo right)
        {
            if (left == null || right == null) return false;
            try { return left.Module == right.Module && left.MetadataToken == right.MetadataToken; }
            catch { return left == right; }
        }

        private static bool TryGetOperandSize(OperandType type, byte[] il, int offset, out int size)
        {
            size = 0;
            switch (type)
            {
                case OperandType.InlineNone: size = 0; return true;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: size = 1; return true;
                case OperandType.InlineVar: size = 2; return true;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR: size = 4; return true;
                case OperandType.InlineI8:
                case OperandType.InlineR: size = 8; return true;
                case OperandType.InlineSwitch:
                    if (offset + 4 > il.Length) return false;
                    int count = BitConverter.ToInt32(il, offset);
                    if (count < 0 || count > (il.Length - offset - 4) / 4) return false;
                    size = 4 + count * 4;
                    return true;
                default: return false;
            }
        }

        private static Dictionary<short, OpCode> BuildOpCodeTable()
        {
            Dictionary<short, OpCode> result = new Dictionary<short, OpCode>();
            FieldInfo[] fields = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType != typeof(OpCode)) continue;
                OpCode op = (OpCode)fields[i].GetValue(null);
                result[op.Value] = op;
            }
            return result;
        }
    }
}
