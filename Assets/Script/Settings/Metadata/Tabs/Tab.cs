using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Menu.Navigation;
using YARG.Menu.Settings.Visuals;
using YARG.Settings.Types;

namespace YARG.Settings.Metadata
{
    public abstract class Tab
    {
        public string Name { get; }
        public string Icon { get; }

        private readonly IPreviewBuilder _previewBuilder;

        protected Tab(string name, string icon = "Generic", IPreviewBuilder previewBuilder = null)
        {
            Name = name;
            Icon = icon;
            _previewBuilder = previewBuilder;
        }

        public abstract void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup);

        public virtual void OnTabEnter()
        {
        }

        public virtual void OnTabExit()
        {
        }

        public virtual UniTask BuildPreviewWorld(Transform worldContainer)
        {
            if (_previewBuilder is not null)
            {
                return _previewBuilder.BuildPreviewWorld(worldContainer);
            }

            return UniTask.CompletedTask;
        }

        public virtual UniTask BuildPreviewUI(Transform uiContainer)
        {
            if (_previewBuilder is not null)
            {
                return _previewBuilder.BuildPreviewUI(uiContainer);
            }

            return UniTask.CompletedTask;
        }

        public virtual void OnSettingChanged()
        {
        }

        protected static BaseSettingVisual SpawnSettingVisual(ISettingType setting, Transform container)
        {
            // Spawn the setting
            var settingPrefab = Addressables.LoadAssetAsync<GameObject>(setting.AddressableName)
                .WaitForCompletion();
            var go = Object.Instantiate(settingPrefab, container);

            // Set the setting, and cache the object
            return go.GetComponent<BaseSettingVisual>();
        }
    }
}