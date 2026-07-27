using System.Text;
using UnityEditor;
using UnityEngine;

/// Wires a character's Animator, and owns the rig type the whole project
/// depends on.
///
/// These models moved from Generic to **Humanoid**. Generic binds a clip to a
/// specific skeleton by bone name, so every animation had to be authored
/// against this exact rig and no outside clip or character could ever be used.
/// Humanoid maps the skeleton onto a standard human definition instead, which
/// means any Humanoid clip — Mixamo, the Asset Store, Quaternius's animation
/// library — retargets onto these characters, and an outside character can be
/// dropped in beside them.
///
/// The cost is that Humanoid can fail *silently*. If Unity's automapper cannot
/// resolve the skeleton it produces an invalid avatar, the Animator plays
/// nothing, and the character stands in its bind pose looking exactly like a
/// model that was never animated — which is a bug this project has already
/// shipped once. So the conversion is verified here, loudly, rather than
/// assumed.
public static class G1Rig
{
    /// Convert the character FBXs to Humanoid and prove it worked.
    /// Must run before anything instantiates the models: SaveAndReimport in
    /// the middle of a build pulls the asset out from under live instances.
    public static void EnsureAvatars(params string[] fbxPaths)
    {
        foreach (var path in fbxPaths)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter imp)
            {
                Debug.LogError($"G1: no model importer at {path}");
                continue;
            }

            bool already = imp.animationType == ModelImporterAnimationType.Human &&
                           imp.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel;
            if (!already)
            {
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                // the models rest in an A-pose; this rotates the reference pose
                // to T for the avatar definition without touching the mesh
                imp.humanoidOversampling = ModelImporterHumanoidOversampling.X2;
                imp.SaveAndReimport();
                Debug.Log($"G1: converted {System.IO.Path.GetFileName(path)} to Humanoid.");
            }
            Verify(path);
        }
    }

    /// An invalid Humanoid avatar is the failure mode that looks like nothing
    /// at all, so say exactly what is wrong and which bones are missing.
    static bool Verify(string path)
    {
        Avatar avatar = null;
        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
            if (sub is Avatar a) { avatar = a; break; }

        string file = System.IO.Path.GetFileName(path);
        if (avatar == null)
        {
            Debug.LogError($"G1: {file} produced NO avatar — every NPC using it " +
                           "will stand in its bind pose. Check the skeleton has " +
                           "hips/spine/head, both arms and both legs.");
            return false;
        }
        if (!avatar.isValid)
        {
            var sb = new StringBuilder();
            sb.Append($"G1: {file} avatar is INVALID. ");
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp != null && imp.humanDescription.human != null)
                sb.Append($"{imp.humanDescription.human.Length} bones mapped. ");
            sb.Append("Unity could not resolve the skeleton to a human figure — " +
                      "the usual cause is a rest pose too far from T.");
            Debug.LogError(sb.ToString());
            return false;
        }
        if (!avatar.isHuman)
        {
            Debug.LogError($"G1: {file} avatar is valid but NOT humanoid — clips " +
                           "will not retarget onto it.");
            return false;
        }

        var im = AssetImporter.GetAtPath(path) as ModelImporter;
        int mapped = im != null && im.humanDescription.human != null
            ? im.humanDescription.human.Length : 0;
        Debug.Log($"G1: {file} humanoid avatar OK — {mapped} bones mapped.");
        return true;
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
