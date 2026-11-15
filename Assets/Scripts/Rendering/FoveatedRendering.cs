using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Projector.Rendering
{
    // Switches on fixed foveated rendering once the headset's display is running. The OpenXR Foveated Rendering
    // feature (Meta Quest tab) only makes it available; nothing is foveated until a level is set here. Created on
    // its own at startup, so no scene needs wiring, and removed once the level is applied.
    public class FoveatedRendering : MonoBehaviour
    {
        // 0 is off, 1 the strongest (the Quest maps this to its discrete levels). The edges of the lenses are blurry
        // anyway, so the strongest level saves the most GPU time for little visible loss.
        const float Level = 1f;
        // Stop trying after this long (no headset, e.g. in the editor without Link).
        const float GiveUpAfter = 10f;

        static readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var host = new GameObject("Foveated Rendering");
            DontDestroyOnLoad(host);
            host.AddComponent<FoveatedRendering>();
        }

        void Update()
        {
            SubsystemManager.GetSubsystems(displays);
            foreach (var display in displays)
            {
                if (!display.running)
                    continue;
                display.foveatedRenderingLevel = Level;
                Destroy(gameObject);
                return;
            }
            if (Time.unscaledTime > GiveUpAfter)
                Destroy(gameObject);
        }
    }
}
