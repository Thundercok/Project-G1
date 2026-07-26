using UnityEditor;
using UnityEngine;

/// Wires an NPC's Animator so it actually animates.
///
/// The models import as Generic rigs (animationType 2), and a Generic Animator
/// needs its Avatar to map the clip onto the hierarchy. The FBX roots come in
/// without an Animator, so `AddComponent&lt;Animator&gt;()` produced one with a
/// controller but a null avatar — every NPC then stood in its bind pose while
/// the state machine happily ran Idle underneath. Load the avatar off the FBX
/// and hand it over.
public static class G1Rig
{
    /// The character FBXs import as Generic with avatarSetup "None", so Unity
    /// never generates the Avatar the rig needs. Flip the importer and reimport
    /// once, before anything instantiates them — reimporting mid-build would
    /// pull the asset out from under live instances.
    public static void EnsureAvatars(params string[] fbxPaths)
    {
        foreach (var path in fbxPaths)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter imp) continue;
            if (imp.animationType == ModelImporterAnimationType.Generic &&
                imp.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel)
                continue;

            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.SaveAndReimport();
            Debug.Log($"G1: generated a Generic Avatar for {path} — NPCs can animate now.");
        }
    }

    public static Animator Setup(GameObject go, string fbxPath, string controllerPath)
    {
        var anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();

        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        if (ctrl != null) anim.runtimeAnimatorController = ctrl;

        if (anim.avatar == null)
        {
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (sub is Avatar avatar && avatar.isValid) { anim.avatar = avatar; break; }
            if (anim.avatar == null)
                Debug.LogWarning($"G1: no valid Avatar in {fbxPath} — {go.name} will not animate.");
        }

        anim.applyRootMotion = false;    // NavMeshAgent/driver owns the movement
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        return anim;
    }
}
