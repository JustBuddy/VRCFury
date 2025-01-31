using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Component;
using VF.Model;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Inspector {
    public class VRCFuryComponentEditor<T> : UnityEditor.Editor where T : VRCFuryComponent {
        private GameObject dummyObject;

        public sealed override VisualElement CreateInspectorGUI() {
            VisualElement content;
            try {
                content = CreateInspectorGUIUnsafe();
            } catch (Exception e) {
                Debug.LogException(new Exception("Failed to render editor", e));
                content = VRCFuryEditorUtils.Error("Failed to render editor (see unity console)");
            }

            var avatarObject = VRCAvatarUtils.GuessAvatarObject(target as UnityEngine.Component);
            var versionLabel = new Label(SceneViewOverlay.GetOutputString(avatarObject) + " " + VRCFPackageUtils.Version);
            versionLabel.AddToClassList("vfVersionLabel");
            
            var contentWithVersion = new VisualElement();
            contentWithVersion.styleSheets.Add(VRCFuryEditorUtils.GetResource<StyleSheet>("VRCFuryStyle.uss"));
            contentWithVersion.Add(content);
            contentWithVersion.Add(versionLabel);
            return contentWithVersion;
        }

        private VisualElement CreateInspectorGUIUnsafe() {
            if (!(target is UnityEngine.Component c)) {
                return VRCFuryEditorUtils.Error("This isn't a component?");
            }
            if (!(c is T v)) {
                return VRCFuryEditorUtils.Error("Unexpected type?");
            }

            var loadError = v.GetBrokenMessage();
            if (loadError != null) {
                return VRCFuryEditorUtils.Error(
                    $"This VRCFury component failed to load ({loadError}). It's likely that your VRCFury is out of date." +
                    " Please try Tools -> VRCFury -> Update VRCFury. If this doesn't help, let us know on the " +
                    " discord at https://vrcfury.com/discord");
            }
            
            var isInstance = PrefabUtility.IsPartOfPrefabInstance(v);

            var container = new VisualElement();

            container.Add(CreateOverrideLabel());

            if (isInstance) {
                // We prevent users from adding overrides on prefabs, because it does weird things (at least in unity 2019)
                // when you apply modifications to an object that lives within a SerializedReference. Some properties not overridden
                // will just be thrown out randomly, and unity will dump a bunch of errors.
                container.Add(CreatePrefabInstanceLabel(v));
            }

            VisualElement body;
            if (isInstance) {
                var copy = CopyComponent(v);
                var copyGameObject = copy.gameObject;
                try {
                    VRCFury.RunningFakeUpgrade = true;
                    copy.Upgrade();
                    // Note that copy may be deleted here!
                } finally {
                    VRCFury.RunningFakeUpgrade = false;
                }
                // We need to prevent our added children from being bound to
                // the original component by unity
                body = new BindingBlock();
                body.SetEnabled(false);

                var children = copyGameObject.GetComponents<T>();
                if (children.Length != 1) body.Add(VRCFuryComponentHeader.CreateHeaderOverlay("Legacy Multi-Component"));
                foreach (var child in children) {
                    child.gameObjectOverride = v.gameObject;
                    var childSo = new SerializedObject(child);
                    var childEditor = _CreateEditor(childSo, child);
                    if (children.Length > 1) childEditor.AddToClassList("vrcfMultipleHeaders");
                    childEditor.Bind(childSo);
                    body.Add(childEditor); 
                }
            } else {
                v.Upgrade();
                if (v == null) return new VisualElement();
                serializedObject.Update();
                body = _CreateEditor(serializedObject, v);
            }
            
            container.Add(body);
            
#if UNITY_2022_1_OR_NEWER
            var editingPrefab = PrefabStageUtility.GetCurrentPrefabStage() != null;
#else
            var editingPrefab = false;
#endif

            var notInAvatarError = VRCFuryEditorUtils.Error(
                "This VRCFury component is not placed on an avatar, and thus will not do anything! " +
                "If you intended to include this in your avatar, make sure you've placed it within your avatar's " +
                "object, and not just alongside it in the scene.");
            void UpdateNotInAvatarError() => notInAvatarError.SetVisible(!editingPrefab && c.gameObject.asVf().GetComponentInSelfOrParent<VRCAvatarDescriptor>() == null);
            UpdateNotInAvatarError();
            notInAvatarError.schedule.Execute(UpdateNotInAvatarError).Every(1000);
            container.Add(notInAvatarError);
            
            return container;
        }

        private C CopyComponent<C>(C original) where C : UnityEngine.Component {
            OnDestroy();
            dummyObject = new GameObject();
            dummyObject.SetActive(false);
            dummyObject.hideFlags |= HideFlags.HideAndDontSave;
            var copy = dummyObject.AddComponent<C>();
            UnitySerializationUtils.CloneSerializable(original, copy);
            return copy;
        }

        public void OnDestroy() {
            if (dummyObject) {
                DestroyImmediate(dummyObject);
            }
        }
        
        private VisualElement _CreateEditor(SerializedObject serializedObject, T target) {
            if (target is VRCFury) {
                return CreateEditor(serializedObject, target);
            }

            var output = new VisualElement();
            var type = target.GetType();
            var attr = type.GetCustomAttribute<AddComponentMenu>();
            string title;
            if (attr != null) {
                title = attr.componentMenu;
                title = Regex.Replace(title, @".*/", "");
                title = Regex.Replace(title, @"^vrcfury[^a-zA-Z0-9]*", "", RegexOptions.IgnoreCase);
            } else {
                title = target.GetType().Name;
                title = Regex.Replace(title, @"(a-z)([A-Z])", "$1 $2");
            }
            output.Add(VRCFuryComponentHeader.CreateHeaderOverlay(title));
            output.Add(CreateEditor(serializedObject, target));
            return output;
        }

        protected virtual VisualElement CreateEditor(SerializedObject serializedObject, T target) {
            return new VisualElement();
        }
        
        private VisualElement CreateOverrideLabel() {
            var baseText = "The VRCFury features in this prefab are overridden on this instance. Please revert them!" +
                           " If you apply, it may corrupt data in the changed features.";
            var overrideLabel = VRCFuryEditorUtils.Error(baseText);
            overrideLabel.SetVisible(false);

            double lastCheck = 0;
            void CheckOverride() {
                if (this == null) return; // The editor was deleted
                var vrcf = (VRCFuryComponent)target;
                var now = EditorApplication.timeSinceStartup;
                if (lastCheck < now - 1) {
                    lastCheck = now;
                    var mods = VRCFPrefabFixer.GetModifications(vrcf);
                    var isModified = mods.Count > 0;
                    overrideLabel.SetVisible(isModified);
                    if (isModified) {
                        overrideLabel.Clear();
                        overrideLabel.Add(VRCFuryEditorUtils.WrappedLabel(baseText + "\n\n" + string.Join(", ", mods.Select(m => m.propertyPath))));
                    }
                }
                EditorApplication.delayCall += CheckOverride;
            }
            CheckOverride();

            return overrideLabel;
        }

        private VisualElement CreatePrefabInstanceLabel(UnityEngine.Component component) {
            void Open() {
                var componentInBasePrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(component);
                var prefabPath = AssetDatabase.GetAssetPath(componentInBasePrefab);
                UnityCompatUtils.OpenPrefab(prefabPath, component.owner());
            }

            var row = new VisualElement().Row();
            row.Add(new VisualElement().FlexGrow(1));

            var label = new Button()
                .OnClick(Open)
                .Text("Edit in Prefab")
                .TextAlign(TextAnchor.MiddleCenter)
                .TextWrap()
                .Padding(3, 5)
                .BorderColor(Color.black)
                .BorderRadius(5)
                .Margin(0, 10)
                .Border(1);
            label.style.borderTopRightRadius = 0;
            label.style.borderTopLeftRadius = 0;
            label.style.marginTop = -2;
            label.style.borderTopWidth = 0;
            row.Add(label);
            return row;
        }
    }
}
