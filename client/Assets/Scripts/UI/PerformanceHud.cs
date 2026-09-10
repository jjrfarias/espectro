using UnityEngine;
using UnityEngine.UI;

namespace Espectro.Prototype
{
    public sealed class PerformanceHud : MonoBehaviour
    {
        [SerializeField] private Text label;
        private float accumulatedTime;
        private int frames;

        public void Configure(Text target) => label = target;

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private void Update()
        {
            accumulatedTime += Time.unscaledDeltaTime;
            frames++;
            if (accumulatedTime < 0.5f || label == null)
            {
                return;
            }

            var fps = frames / accumulatedTime;
            label.text = $"CORTE 0  |  {fps:0} FPS\nAndroid 10+  •  Meta: 30 FPS";
            accumulatedTime = 0f;
            frames = 0;
        }
    }
}
