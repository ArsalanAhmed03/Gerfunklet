using UnityEngine;
using Unity.Netcode.Components;
using Unity.Netcode;

namespace Unity.Multiplayer.Center.NetcodeForGameObjectsExample
{
    /// <summary>
    /// Used for syncing a transform with client side changes. This includes host. Pure server as owner isn't supported by this. Please use NetworkTransform
    /// for transforms that'll always be owned by the server.
    ///
    /// If you want to modify this Script please copy it into your own project and add it to your Player Prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkAnimator : NetworkAnimator
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
