using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Menu.Navigation;

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
    }
}