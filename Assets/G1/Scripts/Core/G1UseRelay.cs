using UnityEngine;

// Split out of G1AccessControl.cs so the class name matches the file name.
//
// Unity only creates a MonoScript for the type whose name matches its file. Any
// other MonoBehaviour in that file can still be added by AddComponent while the
// editor session lasts, and then serialises into the scene as `m_Script:
// {fileID: 0}` — a component that silently is not there the next time the
// scene is opened. Twenty-eight of them had accumulated, including every quest
// zone, every objective-on-death and the extraction gate, which is why walking
// into a quest trigger did nothing and why killing the boss never ticked the
// objective. It fails without an error at any point, so the only defence is the
// naming rule.

/// Access control: the card readers, call buttons and override switches that
/// make Cradle Station's doors feel like doors rather than walls with a state.
///
/// The rule the level teaches, in order: a sealed door has a reader beside it;
/// a reader opens what it is wired to; and one switch in the turbine hall is
/// wired to all of them. Nothing here is explained in text — it is learnt by
/// walking up to the first locked shutter and looking to the right of it.

/// Forwards a use to something standing somewhere else.
///
/// A lift is a hole in a building with a platform in it, and nobody presses a
/// platform. The call button gets the collider and the prompt; this hands the
/// press on to the lift.
public sealed class G1UseRelay : MonoBehaviour, IUsable
{
    public MonoBehaviour target;      // anything implementing IUsable

    public void OnUse(GameObject user)
    {
        if (target is IUsable u) u.OnUse(user);
    }
}
