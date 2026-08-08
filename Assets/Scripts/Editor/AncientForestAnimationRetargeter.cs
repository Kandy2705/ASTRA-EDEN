#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Retargets animations authored on the a_tyran_01 (E*) rig onto the RaptorALL
/// (Bip01) skeleton used by the Ancient Forest boss.
///
/// ROTATION ONLY. Rotation is corrected through rest poses instead of copying
/// raw quaternions: targetRotation = targetRest * (sourceRest^-1 * sourceAnimatedRotation),
/// with sign-flip continuity applied across frames.
///
/// NO position or scale curves are generated: the source and target rigs have
/// different bone lengths/offsets, so copying raw localPosition/localScale would
/// collapse the boss. The target skeleton keeps its own localPosition/localScale,
/// and the root (Hips) keeps its rest-pose compatible position - movement is left
/// to EnemyAIController/NavMeshAgent (Apply Root Motion stays off).
///
/// The PlayYelp event, frame rate, duration and non-loop behaviour are preserved,
/// and every mapping is logged.
///
/// LEGS COME FROM IDLE: no source leg motion is retargeted. Instead the leg
/// hierarchy (Bip01_[L/R]_Thigh/Calf/HorseLink/Foot/Toe0/Toe1/Toe2 and all
/// descendants such as Toe01/Toe11/Toe21/Nub) is populated by copying the
/// rotation curves of the same bones from AncientForest_Idle.anim, so the feet
/// stay planted as in the idle stance.
///
/// WEIGHT REACTION: Hips and Bip01_Pelvis are NOT fully animated. Their rotation
/// is scaled to ~RootReactionScale (15%) of the source root delta via
/// Quaternion.Slerp, so the pelvis shifts weight very slightly instead of the
/// whole body swinging around the planted feet. The hit reaction lives mostly in
/// Spine -> Spine1 -> Neck -> Neck1 -> Head (full rotation), while the legs and
/// feet keep their world position/foot lock.
/// </summary>
public static class AncientForestAnimationRetargeter
{
    const string SourceClipPath = "Assets/Animations/Motion/Boss/hit_to_stun.anim";
    const string IdleClipPath = "Assets/Animations/Enemy_Boss_AncientForest/AncientForest_Idle.anim";
    const string SourceModelPath =
        "Assets/Prefabs/Enemy/dino-hunter-deadly-shores-vicious/source/a_tyran_01.fbx";
    const string TargetModelPath = "Assets/Prefabs/Enemy/tyranno/source/RaptorALL.fbx";
    const string BossPrefabPath = "Assets/_Project/Prefab/Enemy_Boss_AncientForest.prefab";
    const string OutputFolder = "Assets/Animations/Enemy_Boss_AncientForest/Retargeted";
    const string OutputClipPath = OutputFolder + "/AncientForest_HitToStun_Retargeted.anim";
    const string AnimatorPath = "Assets/Animations/Enemy_Boss_AncientForest_Animator.controller";
    const string HitStateName = "Hit";

    const string SourceBonePrefix = "a_tyran_01/";
    const string TargetRootPath = "Hips";

    const float RootReactionScale = 0.15f;

    const string Pelvis = "Hips/Bip01_Pelvis";
    const string Spine = Pelvis + "/Bip01_Spine";
    const string Spine1 = Spine + "/Bip01_Spine1";
    const string Neck = Spine1 + "/Bip01_Neck";
    const string Neck1 = Neck + "/Bip01_Neck1";
    const string Head = Neck1 + "/Bip01_Head";

    static readonly Dictionary<string, string> BoneMap = new Dictionary<string, string>
    {
        ["ERoot_M"] = TargetRootPath,

        ["EBackA_M"] = Spine,
        ["EBackB_M"] = Spine1,
        ["ENeck_M"] = Neck,
        ["ENeck2"] = Neck1,
        ["EHead_M"] = Head,

        ["EScapula_L"] = Neck + "/Bip01_L_Clavicle",
        ["EShoulder_L"] = Neck + "/Bip01_L_Clavicle/Bip01_L_UpperArm",
        ["EElbow_L"] = Neck + "/Bip01_L_Clavicle/Bip01_L_UpperArm/Bip01_L_Forearm",
        ["EWrist_L"] = Neck + "/Bip01_L_Clavicle/Bip01_L_UpperArm/Bip01_L_Forearm/Bip01_L_Hand",
        ["EIndexFinger1_L"] = Neck + "/Bip01_L_Clavicle/Bip01_L_UpperArm/Bip01_L_Forearm/Bip01_L_Hand/Bip01_L_Finger0",

        ["EScapula_R"] = Neck + "/Bip01_R_Clavicle",
        ["EShoulder_R"] = Neck + "/Bip01_R_Clavicle/Bip01_R_UpperArm",
        ["EElbow_R"] = Neck + "/Bip01_R_Clavicle/Bip01_R_UpperArm/Bip01_R_Forearm",
        ["EWrist_R"] = Neck + "/Bip01_R_Clavicle/Bip01_R_UpperArm/Bip01_R_Forearm/Bip01_R_Hand",
        ["EIndexFinger1_R"] = Neck + "/Bip01_R_Clavicle/Bip01_R_UpperArm/Bip01_R_Forearm/Bip01_R_Hand/Bip01_R_Finger0",

        ["ERump_L"] = Pelvis + "/Bip01_L_Thigh",
        ["EbackKnee_L"] = Pelvis + "/Bip01_L_Thigh/Bip01_L_Calf",
        ["EAnkle_L"] = Pelvis + "/Bip01_L_Thigh/Bip01_L_Calf/Bip01_L_HorseLink",
        ["EMiddleToe1_L"] = Pelvis + "/Bip01_L_Thigh/Bip01_L_Calf/Bip01_L_HorseLink/Bip01_L_Foot",
        ["ELToe_01_01"] = Pelvis + "/Bip01_L_Thigh/Bip01_L_Calf/Bip01_L_HorseLink/Bip01_L_Foot/Bip01_L_Toe0",
        ["ELToe_01_02"] = Pelvis + "/Bip01_L_Thigh/Bip01_L_Calf/Bip01_L_HorseLink/Bip01_L_Foot/Bip01_L_Toe0/Bip01_L_Toe01",
        ["ELToe_02_01"] = Pelvis + "/Bip01_L_Thigh/Bip01_L_Calf/Bip01_L_HorseLink/Bip01_L_Foot/Bip01_L_Toe1",
        ["ELToe_02_02"] = Pelvis + "/Bip01_L_Thigh/Bip01_L_Calf/Bip01_L_HorseLink/Bip01_L_Foot/Bip01_L_Toe1/Bip01_L_Toe11",
        ["ELToe_02_03"] = Pelvis + "/Bip01_L_Thigh/Bip01_L_Calf/Bip01_L_HorseLink/Bip01_L_Foot/Bip01_L_Toe1/Bip01_L_Toe11/Bip01_L_Toe1Nub",
        ["ELToe_03_01"] = Pelvis + "/Bip01_L_Thigh/Bip01_L_Calf/Bip01_L_HorseLink/Bip01_L_Foot/Bip01_L_Toe2",
        ["ELToe_03_02"] = Pelvis + "/Bip01_L_Thigh/Bip01_L_Calf/Bip01_L_HorseLink/Bip01_L_Foot/Bip01_L_Toe2/Bip01_L_Toe21",

        ["ERump_R"] = Pelvis + "/Bip01_R_Thigh",
        ["EbackKnee_R"] = Pelvis + "/Bip01_R_Thigh/Bip01_R_Calf",
        ["EAnkle_R"] = Pelvis + "/Bip01_R_Thigh/Bip01_R_Calf/Bip01_R_HorseLink",
        ["EMiddleToe1_R"] = Pelvis + "/Bip01_R_Thigh/Bip01_R_Calf/Bip01_R_HorseLink/Bip01_R_Foot",
        ["ERToe_01_01"] = Pelvis + "/Bip01_R_Thigh/Bip01_R_Calf/Bip01_R_HorseLink/Bip01_R_Foot/Bip01_R_Toe0",
        ["ERToe_01_02"] = Pelvis + "/Bip01_R_Thigh/Bip01_R_Calf/Bip01_R_HorseLink/Bip01_R_Foot/Bip01_R_Toe0/Bip01_R_Toe01",
        ["ERToe_02_01"] = Pelvis + "/Bip01_R_Thigh/Bip01_R_Calf/Bip01_R_HorseLink/Bip01_R_Foot/Bip01_R_Toe1",
        ["ERToe_02_02"] = Pelvis + "/Bip01_R_Thigh/Bip01_R_Calf/Bip01_R_HorseLink/Bip01_R_Foot/Bip01_R_Toe1/Bip01_R_Toe11",
        ["joint3"] = Pelvis + "/Bip01_R_Thigh/Bip01_R_Calf/Bip01_R_HorseLink/Bip01_R_Foot/Bip01_R_Toe1/Bip01_R_Toe11/Bip01_R_Toe1Nub",
        ["ERToe_03_01"] = Pelvis + "/Bip01_R_Thigh/Bip01_R_Calf/Bip01_R_HorseLink/Bip01_R_Foot/Bip01_R_Toe2",
        ["ERToe_03_02"] = Pelvis + "/Bip01_R_Thigh/Bip01_R_Calf/Bip01_R_HorseLink/Bip01_R_Foot/Bip01_R_Toe2/Bip01_R_Toe21",

        ["ETail0_M"] = Pelvis + "/Bip01_Tail",
        ["ETail1_M"] = Pelvis + "/Bip01_Tail/Bip01_Tail1",
        ["ETail2_M"] = Pelvis + "/Bip01_Tail/Bip01_Tail1/Bip01_Tail2",
        ["ETail3_M"] = Pelvis + "/Bip01_Tail/Bip01_Tail1/Bip01_Tail2/Bip01_Tail3",
        ["ETail4_M"] = Pelvis + "/Bip01_Tail/Bip01_Tail1/Bip01_Tail2/Bip01_Tail3/Bip01_Tail4",
    };

    static readonly Dictionary<string, string> SkipReasons = new Dictionary<string, string>
    {
        ["EChest_M"] = "target has no chest bone between Bip01_Spine1 and Bip01_Neck",
        ["EJaw_M"] = "RaptorALL has no jaw bone",
        ["EbackHip_L"] = "skipped to avoid double-driving Bip01_L_Calf (ERump_L drives the thigh)",
        ["EbackHip_R"] = "skipped to avoid double-driving Bip01_R_Calf (ERump_R drives the thigh)",
        ["ERingFinger1_L"] = "target only has an index finger (Bip01_L_Finger0)",
        ["ERingFinger1_R"] = "target only has an index finger (Bip01_R_Finger0)",
        ["ETail5_M"] = "skipped to avoid double-driving Bip01_TailNub",
        ["ETail6_M"] = "skipped to avoid double-driving Bip01_TailNub",
        ["ELRumpClavicle"] = "target has no rump-clavicle bone",
        ["ERRumpClavicle"] = "target has no rump-clavicle bone",
    };

    static readonly HashSet<string> SourceLegBones = new HashSet<string>
    {
        "ERump_L",
        "ERump_R",
        "EbackHip_L",
        "EbackHip_R",
        "EbackKnee_L",
        "EbackKnee_R",
        "EAnkle_L",
        "EAnkle_R",
        "EMiddleToe1_L",
        "EMiddleToe1_R",
    };

    static readonly HashSet<string> LockedLegBones = new HashSet<string>
    {
        "Bip01_L_Thigh",
        "Bip01_L_Calf",
        "Bip01_L_HorseLink",
        "Bip01_L_Foot",
        "Bip01_L_Toe0",
        "Bip01_L_Toe1",
        "Bip01_L_Toe2",
        "Bip01_R_Thigh",
        "Bip01_R_Calf",
        "Bip01_R_HorseLink",
        "Bip01_R_Foot",
        "Bip01_R_Toe0",
        "Bip01_R_Toe1",
        "Bip01_R_Toe2",
    };

    static bool IsSourceLegBone(string boneName)
    {
        if (SourceLegBones.Contains(boneName)) return true;
        if (boneName.StartsWith("ELToe_", StringComparison.Ordinal)) return true;
        if (boneName.StartsWith("ERToe_", StringComparison.Ordinal)) return true;
        if (boneName.StartsWith("joint3", StringComparison.Ordinal)) return true;
        return false;
    }

    static bool IsTargetLegPath(string path)
    {
        return path.Split('/').Any(segment => LockedLegBones.Contains(segment));
    }

    [MenuItem("ASTRA EDEN/Animation/Retarget To Ancient Forest Boss")]
    public static void Retarget()
    {
        AnimationClip sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClipPath);
        if (sourceClip == null)
        {
            Debug.LogError($"[Retarget] Source clip not found: {SourceClipPath}");
            return;
        }

        Dictionary<string, Quaternion> sourceRest = BuildSourceRestPose();
        Dictionary<string, Quaternion> targetRest = BuildTargetRestPose();

        AnimationClip output = BuildRetargetedClip(sourceClip, sourceRest, targetRest);
        if (output == null) return;

        AssignToHitState(output);

        bool valid = Validate(output);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(valid
            ? $"[Retarget] Done. Output: {OutputClipPath} | Hit state updated | validation passed."
            : $"[Retarget] Done but validation reported issues. Output: {OutputClipPath}");
    }

    public static void RetargetBatch()
    {
        Retarget();
        EditorApplication.Exit(0);
    }

    static Dictionary<string, Quaternion> BuildSourceRestPose()
    {
        var result = new Dictionary<string, Quaternion>();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
        if (model == null)
        {
            Debug.LogError($"[Retarget] Source model not found: {SourceModelPath}");
            return result;
        }

        foreach (Transform transform in model.GetComponentsInChildren<Transform>(true))
        {
            if (!result.ContainsKey(transform.name))
            {
                result[transform.name] = transform.localRotation;
            }
        }

        return result;
    }

    static Dictionary<string, Quaternion> BuildTargetRestPose()
    {
        var result = new Dictionary<string, Quaternion>();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(TargetModelPath);
        if (model == null)
        {
            Debug.LogError($"[Retarget] Target model not found: {TargetModelPath}");
            return result;
        }

        Transform hips = FindChildRecursive(model.transform, TargetRootPath);
        if (hips == null)
        {
            Debug.LogError($"[Retarget] '{TargetRootPath}' not found in {TargetModelPath}");
            return result;
        }

        foreach (Transform transform in hips.GetComponentsInChildren<Transform>(true))
        {
            string path = transform == hips
                ? TargetRootPath
                : TargetRootPath + "/" + AnimationUtility.CalculateTransformPath(transform, hips);
            result[path] = transform.localRotation;
        }

        return result;
    }

    static Transform FindChildRecursive(Transform root, string name)
    {
        if (string.Equals(root.name, name, StringComparison.Ordinal)) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }

    static AnimationClip BuildRetargetedClip(
        AnimationClip sourceClip,
        Dictionary<string, Quaternion> sourceRest,
        Dictionary<string, Quaternion> targetRest)
    {
        var byPath = new Dictionary<string, Dictionary<string, AnimationCurve>>();
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(sourceClip))
        {
            if (binding.type != typeof(Transform) || binding.isPPtrCurve) continue;
            if (!IsSupportedProperty(binding.propertyName)) continue;

            if (!byPath.TryGetValue(binding.path, out Dictionary<string, AnimationCurve> components))
            {
                components = new Dictionary<string, AnimationCurve>();
                byPath[binding.path] = components;
            }

            AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, binding);
            if (curve != null)
            {
                components[binding.propertyName] = curve;
            }
        }

        float frameRate = Mathf.Max(1f, sourceClip.frameRate);
        float length = Mathf.Max(0.001f, sourceClip.length);

        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        if (idleClip == null)
        {
            Debug.LogWarning($"[Retarget] Idle clip not found: {IdleClipPath} - legs will stay in bind pose instead of idle stance.");
        }

        var newClip = new AnimationClip
        {
            name = "AncientForest_HitToStun_Retargeted",
            frameRate = frameRate,
        };

        var mapped = new List<string>();
        var skipped = new List<string>();
        var legSkips = new List<string>();
        var legIdle = new List<string>();

        foreach (KeyValuePair<string, Dictionary<string, AnimationCurve>> entry in byPath)
        {
            string sourcePath = entry.Key;
            if (!sourcePath.StartsWith(SourceBonePrefix, StringComparison.Ordinal))
            {
                skipped.Add($"{sourcePath}  ->  skipped (outside source prefix)");
                continue;
            }

            string relativePath = sourcePath.Substring(SourceBonePrefix.Length);
            string leaf = LastSegment(relativePath);

            if (IsSourceLegBone(leaf))
            {
                string legTarget = BoneMap.TryGetValue(leaf, out string mappedLeg)
                    ? mappedLeg
                    : "<no target>";
                legSkips.Add($"{leaf} -> {legTarget}");
                continue;
            }

            if (!BoneMap.TryGetValue(leaf, out string targetPath))
            {
                string reason = SkipReasons.TryGetValue(leaf, out string skipReason)
                    ? $"skipped: {skipReason}"
                    : "NO TARGET BONE";
                skipped.Add($"{sourcePath}  ->  {reason}");
                continue;
            }

            if (IsTargetLegPath(targetPath))
            {
                legSkips.Add($"{leaf} -> {targetPath}");
                continue;
            }

            if (!targetRest.TryGetValue(targetPath, out Quaternion targetBoneRest))
            {
                skipped.Add($"{sourcePath}  ->  target rest pose missing for {targetPath}");
                continue;
            }

            var components = entry.Value;
            bool hasQuaternion =
                components.ContainsKey("m_LocalRotation.x") &&
                components.ContainsKey("m_LocalRotation.y") &&
                components.ContainsKey("m_LocalRotation.z") &&
                components.ContainsKey("m_LocalRotation.w");

            if (hasQuaternion)
            {
                if (!sourceRest.TryGetValue(leaf, out Quaternion sourceBoneRest))
                {
                    skipped.Add($"{sourcePath}  ->  source rest pose missing for {leaf}");
                    continue;
                }

                if (string.Equals(leaf, "ERoot_M", StringComparison.Ordinal))
                {
                    (AnimationCurve hx, AnimationCurve hy, AnimationCurve hz, AnimationCurve hw) =
                        BuildCorrectedRotationCurves(components, sourceBoneRest, targetBoneRest, frameRate, length, RootReactionScale);

                    SetCurve(newClip, targetPath, "m_LocalRotation.x", hx);
                    SetCurve(newClip, targetPath, "m_LocalRotation.y", hy);
                    SetCurve(newClip, targetPath, "m_LocalRotation.z", hz);
                    SetCurve(newClip, targetPath, "m_LocalRotation.w", hw);

                    if (targetRest.TryGetValue(Pelvis, out Quaternion pelvisRest))
                    {
                        (AnimationCurve px, AnimationCurve py, AnimationCurve pz, AnimationCurve pw) =
                            BuildCorrectedRotationCurves(components, sourceBoneRest, pelvisRest, frameRate, length, RootReactionScale);

                        SetCurve(newClip, Pelvis, "m_LocalRotation.x", px);
                        SetCurve(newClip, Pelvis, "m_LocalRotation.y", py);
                        SetCurve(newClip, Pelvis, "m_LocalRotation.z", pz);
                        SetCurve(newClip, Pelvis, "m_LocalRotation.w", pw);
                    }

                    mapped.Add($"ERoot_M  ->  {targetPath} + {Pelvis} (weight reaction ~{Mathf.RoundToInt(RootReactionScale * 100f)}%)");
                    continue;
                }

                (AnimationCurve x, AnimationCurve y, AnimationCurve z, AnimationCurve w) =
                    BuildCorrectedRotationCurves(components, sourceBoneRest, targetBoneRest, frameRate, length);

                SetCurve(newClip, targetPath, "m_LocalRotation.x", x);
                SetCurve(newClip, targetPath, "m_LocalRotation.y", y);
                SetCurve(newClip, targetPath, "m_LocalRotation.z", z);
                SetCurve(newClip, targetPath, "m_LocalRotation.w", w);

                mapped.Add($"{leaf}  ->  {targetPath}");
            }
        }

        if (idleClip != null)
        {
            Dictionary<string, Dictionary<string, AnimationCurve>> idleLegCurves = CollectIdleLegRotationCurves(idleClip);
            foreach (KeyValuePair<string, Dictionary<string, AnimationCurve>> legEntry in idleLegCurves)
            {
                foreach (KeyValuePair<string, AnimationCurve> kvp in legEntry.Value)
                {
                    SetCurve(newClip, legEntry.Key, kvp.Key, ResampleCurve(kvp.Value, frameRate, length));
                }

                legIdle.Add(legEntry.Key);
            }
        }

        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(sourceClip);
        if (events != null && events.Length > 0)
        {
            AnimationUtility.SetAnimationEvents(newClip, events);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(sourceClip);
        settings.loopTime = false;
        settings.loopBlend = false;
        settings.keepOriginalOrientation = true;
        settings.keepOriginalPositionXZ = true;
        settings.keepOriginalPositionY = true;
        settings.cycleOffset = 0f;
        settings.level = 0f;
        AnimationUtility.SetAnimationClipSettings(newClip, settings);

        EnsureFolder(OutputFolder);
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputClipPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(newClip, OutputClipPath);
        }
        else
        {
            EditorUtility.CopySerialized(newClip, existing);
            UnityEngine.Object.DestroyImmediate(newClip);
        }

        AnimationClip saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputClipPath);
        if (saved == null)
        {
            Debug.LogError($"[Retarget] Failed to write output clip: {OutputClipPath}");
            return null;
        }

        EditorUtility.SetDirty(saved);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Retarget] Created {OutputClipPath} ({mapped.Count} bones mapped, {legIdle.Count} leg bones from Idle).");
        Debug.Log("[Retarget] Bone mapping:\n" + string.Join("\n", mapped));
        Debug.Log("[Retarget] Skipped/unmapped:\n" + string.Join("\n", skipped));
        if (legSkips.Count > 0)
        {
            Debug.Log("[Retarget] SKIP LEG:\n" + string.Join("\n", legSkips));
        }
        if (legIdle.Count > 0)
        {
            Debug.Log("[Retarget] LEG FROM IDLE:\n" + string.Join("\n", legIdle));
        }

        return saved;
    }

    static (AnimationCurve, AnimationCurve, AnimationCurve, AnimationCurve) BuildCorrectedRotationCurves(
        Dictionary<string, AnimationCurve> components,
        Quaternion sourceRest,
        Quaternion targetRest,
        float frameRate,
        float length,
        float rotationScale = 1f)
    {
        int count = Mathf.Max(2, Mathf.FloorToInt(length * frameRate) + 1);
        float step = 1f / frameRate;

        var keysX = new Keyframe[count];
        var keysY = new Keyframe[count];
        var keysZ = new Keyframe[count];
        var keysW = new Keyframe[count];

        Quaternion previousRotation = Quaternion.identity;
        for (int i = 0; i < count; i++)
        {
            float t = i * step;
            Quaternion sourceRotation = new Quaternion(
                components["m_LocalRotation.x"].Evaluate(t),
                components["m_LocalRotation.y"].Evaluate(t),
                components["m_LocalRotation.z"].Evaluate(t),
                components["m_LocalRotation.w"].Evaluate(t)).normalized;

            Quaternion delta = Quaternion.Inverse(sourceRest) * sourceRotation;
            Quaternion scaledDelta = rotationScale >= 1f
                ? delta
                : Quaternion.Slerp(Quaternion.identity, delta, rotationScale);
            Quaternion targetRotation = (targetRest * scaledDelta).normalized;

            if (i > 0 && Quaternion.Dot(targetRotation, previousRotation) < 0f)
            {
                targetRotation = new Quaternion(-targetRotation.x, -targetRotation.y, -targetRotation.z, -targetRotation.w);
            }

            previousRotation = targetRotation;

            keysX[i] = new Keyframe(t, targetRotation.x);
            keysY[i] = new Keyframe(t, targetRotation.y);
            keysZ[i] = new Keyframe(t, targetRotation.z);
            keysW[i] = new Keyframe(t, targetRotation.w);
        }

        return (
            new AnimationCurve(keysX),
            new AnimationCurve(keysY),
            new AnimationCurve(keysZ),
            new AnimationCurve(keysW));
    }

    static Dictionary<string, Dictionary<string, AnimationCurve>> CollectIdleLegRotationCurves(AnimationClip idleClip)
    {
        var result = new Dictionary<string, Dictionary<string, AnimationCurve>>();
        if (idleClip == null) return result;

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(idleClip))
        {
            if (binding.type != typeof(Transform) || binding.isPPtrCurve) continue;
            if (!binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal)) continue;
            if (!IsTargetLegPath(binding.path)) continue;

            AnimationCurve curve = AnimationUtility.GetEditorCurve(idleClip, binding);
            if (curve == null) continue;

            if (!result.TryGetValue(binding.path, out Dictionary<string, AnimationCurve> components))
            {
                components = new Dictionary<string, AnimationCurve>();
                result[binding.path] = components;
            }

            components[binding.propertyName] = curve;
        }

        return result;
    }

    static AnimationCurve ResampleCurve(AnimationCurve source, float frameRate, float length)
    {
        int count = Mathf.Max(2, Mathf.FloorToInt(length * frameRate) + 1);
        float step = 1f / frameRate;

        var keys = new Keyframe[count];
        for (int i = 0; i < count; i++)
        {
            float t = i * step;
            keys[i] = new Keyframe(t, source.Evaluate(t));
        }

        return new AnimationCurve(keys);
    }

    static void AssignToHitState(AnimationClip clip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorPath);
        if (controller == null)
        {
            Debug.LogError($"[Retarget] Animator controller not found: {AnimatorPath}");
            return;
        }

        if (controller.layers.Length == 0)
        {
            Debug.LogError("[Retarget] Animator controller has no layers.");
            return;
        }

        AnimatorState hit = FindState(controller, HitStateName);
        if (hit == null)
        {
            Debug.LogError($"[Retarget] '{HitStateName}' state not found in {AnimatorPath}");
            return;
        }

        hit.motion = clip;
        EditorUtility.SetDirty(controller);
        Debug.Log($"[Retarget] {HitStateName} state now uses {clip.name}. Hit/Stagger transitions untouched.");
    }

    static bool Validate(AnimationClip outputClip)
    {
        bool valid = true;

        if (outputClip == null || AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputClipPath) == null)
        {
            Debug.LogError("[Retarget] Validation: output clip is missing.");
            return false;
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorPath);
        if (controller == null || controller.layers.Length == 0)
        {
            Debug.LogError("[Retarget] Validation: animator controller missing or has no layers.");
            return false;
        }

        AnimatorState hit = FindState(controller, HitStateName);
        if (hit == null)
        {
            Debug.LogError($"[Retarget] Validation: '{HitStateName}' state not found.");
            valid = false;
        }
        else if (hit.motion != outputClip)
        {
            Debug.LogError("[Retarget] Validation: Hit state is not using the retargeted clip.");
            valid = false;
        }

        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(outputClip);
        if (events == null || !events.Any(animationEvent => animationEvent.functionName == "PlayYelp"))
        {
            Debug.LogError("[Retarget] Validation: PlayYelp event missing from output clip.");
            valid = false;
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(outputClip);
        if (settings.loopTime)
        {
            Debug.LogError("[Retarget] Validation: output clip must be non-looping.");
            valid = false;
        }

        HashSet<string> targetBones = new HashSet<string>(BuildTargetRestPose().Keys);
        HashSet<string> sourceBoneNames = new HashSet<string>(BoneMap.Keys.Concat(SkipReasons.Keys));

        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        HashSet<string> idleLegPaths = new HashSet<string>(CollectIdleLegRotationCurves(idleClip).Keys);

        var legBindingsInOutput = new HashSet<string>();
        var allBindingsInOutput = new HashSet<string>();
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(outputClip))
        {
            string path = binding.path;
            allBindingsInOutput.Add(path);

            if (!binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal))
            {
                Debug.LogError($"[Retarget] Validation: curve property must be rotation-only, found '{binding.propertyName}' on {path}");
                valid = false;
            }

            if (path.StartsWith(SourceBonePrefix, StringComparison.Ordinal) ||
                path.Split('/').Any(segment => sourceBoneNames.Contains(segment)))
            {
                Debug.LogError($"[Retarget] Validation: curve path still points to a source E* bone: {path}");
                valid = false;
            }

            if (IsTargetLegPath(path))
            {
                legBindingsInOutput.Add(path);
                if (!idleLegPaths.Contains(path))
                {
                    Debug.LogError($"[Retarget] Validation: leg curve must come from AncientForest_Idle.anim, but '{path}' is not animated there.");
                    valid = false;
                }
            }

            if (!targetBones.Contains(path))
            {
                Debug.LogError($"[Retarget] Validation: curve path missing in RaptorALL hierarchy: {path}");
                valid = false;
            }
        }

        if (idleClip != null)
        {
            string leftThigh = Pelvis + "/Bip01_L_Thigh";
            string rightThigh = Pelvis + "/Bip01_R_Thigh";
            if (!legBindingsInOutput.Contains(leftThigh) || !legBindingsInOutput.Contains(rightThigh))
            {
                Debug.LogError("[Retarget] Validation: legs must be driven by Idle stance (expected Bip01_L_Thigh and Bip01_R_Thigh curves).");
                valid = false;
            }
        }

        if (!allBindingsInOutput.Contains(TargetRootPath) || !allBindingsInOutput.Contains(Pelvis))
        {
            Debug.LogError("[Retarget] Validation: Hips/Bip01_Pelvis weight-reaction curves are missing from the output clip.");
            valid = false;
        }

        GameObject boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
        if (boss != null)
        {
            Animator animator = boss.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.applyRootMotion)
            {
                Debug.LogError("[Retarget] Validation: Apply Root Motion must remain false on the boss Animator.");
                valid = false;
            }
        }
        else
        {
            Debug.LogWarning($"[Retarget] Validation: boss prefab not found, skipping Apply Root Motion check: {BossPrefabPath}");
        }

        string[] protectedStates = { "Locomotion", "Bite Attack", "Heavy Bite Attack2", "Headbutt", "TailWhip", "Poison Roar", "Death" };
        foreach (AnimatorState state in controller.layers[0].stateMachine.states.Select(child => child.state))
        {
            if (protectedStates.Contains(state.name) && state.motion == outputClip)
            {
                Debug.LogError($"[Retarget] Validation: protected state '{state.name}' must not use the retargeted clip.");
                valid = false;
            }
        }

        return valid;
    }

    static AnimatorState FindState(AnimatorController controller, string stateName)
    {
        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
        {
            if (string.Equals(child.state.name, stateName, StringComparison.Ordinal))
            {
                return child.state;
            }
        }

        return null;
    }

    static void SetCurve(AnimationClip clip, string path, string property, AnimationCurve curve)
    {
        clip.SetCurve(path, typeof(Transform), property, curve);
    }

    static bool IsSupportedProperty(string propertyName)
    {
        return propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal);
    }

    static string LastSegment(string path)
    {
        int index = path.LastIndexOf('/');
        return index >= 0 ? path.Substring(index + 1) : path;
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string name = Path.GetFileName(folder);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
