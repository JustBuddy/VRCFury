using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Model.StateAction;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

// Notes for the future:
// Don't ever remove a class -- it will break the entire list of SerializedReferences that contained it
// Don't mark a class as Obsolete or MovedFrom -- unity 2019 will go into an infinite loop and die

namespace VF.Model.Feature {
    
    [AttributeUsage(AttributeTargets.Class)]
    public class NoBuilder : Attribute {
    }

    [Serializable]
    public abstract class FeatureModel {
    }

    [Serializable]
    public abstract class NewFeatureModel : FeatureModel, ISerializationCallbackReceiver {
        public int version = -1;

        private int GetVersion() {
            return version < 0 ? GetLatestVersion() : version;
        }

        public void Upgrade() {
            var fromVersion = GetVersion();
            var latestVersion = GetLatestVersion();
            if (fromVersion != latestVersion) {
                Upgrade(fromVersion);
                version = latestVersion;
            }
        }

        public virtual void Upgrade(int fromVersion) {
        }

        public virtual int GetLatestVersion() {
            return 0;
        }
        
        public void OnAfterDeserialize() {
            if (version < 0) {
                // Object was deserialized, but had no version. Default to version 0.
                version = 0;
            }
        }

        public void OnBeforeSerialize() {
            if (version < 0) {
                // Object was created fresh (not deserialized), so it's automatically the newest
                version = GetLatestVersion();
            }
        }
    }
    
    /**
     * This class exists because of an annoying unity bug. If a class is serialized with no fields (an empty class),
     * then if you try to deserialize it into a class with fields, unity blows up and fails. This means we can never
     * add fields to a class which started without any. This, all features must be migrated to NewFeatureModel,
     * which contains one field by default (version).
     */
    [Serializable]
    public abstract class LegacyFeatureModel : FeatureModel {
        public abstract NewFeatureModel CreateNewInstance();
    }

    [Serializable]
    public class AvatarScale : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new AvatarScale2();
        }
    }
    [Serializable]
    public class AvatarScale2 : NewFeatureModel {
    }

    [Serializable]
    public class Blinking : NewFeatureModel {
        public State state;
        public float transitionTime = -1;
        public float holdTime = -1;
    }

    [Serializable]
    public class Breathing : NewFeatureModel {
        public State inState;
        public State outState;

        public override void Upgrade(int fromVersion) {
            if (fromVersion < 1) {
                if (obj != null) {
                    inState.actions.Add(new ScaleAction { obj = obj, scale = scaleMin });
                    outState.actions.Add(new ScaleAction { obj = obj, scale = scaleMax });
                    obj = null;
                }

                if (!string.IsNullOrWhiteSpace(blendshape)) {
                    inState.actions.Add(new BlendShapeAction() { blendShape = blendshape, blendShapeValue = 0 });
                    outState.actions.Add(new BlendShapeAction() { blendShape = blendshape, blendShapeValue = 100 });
                    blendshape = null;
                }
            }
        }
        public override int GetLatestVersion() {
            return 1;
        }
        
        // legacy
        public GameObject obj;
        public string blendshape;
        public float scaleMin;
        public float scaleMax;
    }

    [Serializable]
    public class FullController : NewFeatureModel {
        public List<ControllerEntry> controllers = new List<ControllerEntry>();
        public List<MenuEntry> menus = new List<MenuEntry>();
        public List<ParamsEntry> prms = new List<ParamsEntry>();
        public List<string> globalParams = new List<string>();
        public List<string> removePrefixes = new List<string>();
        public string addPrefix = "";
        public bool allNonsyncedAreGlobal = false;
        public bool ignoreSaved;
        public string toggleParam;
        public GameObject rootObjOverride;
        public bool rootBindingsApplyToAvatar;

        // obsolete
        public RuntimeAnimatorController controller;
        public VRCExpressionsMenu menu;
        public VRCExpressionParameters parameters;
        public string submenu;

        [Serializable]
        public class ControllerEntry {
            public RuntimeAnimatorController controller;
            public VRCAvatarDescriptor.AnimLayerType type = VRCAvatarDescriptor.AnimLayerType.FX;
            public bool ResetMePlease;
        }

        [Serializable]
        public class MenuEntry {
            public VRCExpressionsMenu menu;
            public string prefix;
            public bool ResetMePlease;
        }

        [Serializable]
        public class ParamsEntry {
            public VRCExpressionParameters parameters;
            public bool ResetMePlease;
        }

        public override void Upgrade(int fromVersion) {
            if (fromVersion < 1) {
                allNonsyncedAreGlobal = true;
            }
            if (fromVersion < 2) {
                if (controller != null) {
                    controllers.Add(new ControllerEntry { controller = controller });
                    controller = null;
                }
                if (menu != null) {
                    menus.Add(new MenuEntry { menu = menu, prefix = submenu });
                    menu = null;
                }
                if (parameters != null) {
                    prms.Add(new ParamsEntry { parameters = parameters });
                    parameters = null;
                }
            }
        }

        public override int GetLatestVersion() {
            return 2;
        }
    }
    
    // Obsolete and removed
    [Serializable]
    public class LegacyPrefabSupport : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new LegacyPrefabSupport2();
        }
    }
    public class LegacyPrefabSupport2 : NewFeatureModel {
    }

    [Serializable]
    public class ZawooIntegration : NewFeatureModel {
        public string submenu;
    }

    [Serializable]
    public class Modes : NewFeatureModel {
        public string name;
        public bool saved;
        public bool securityEnabled;
        public List<Mode> modes = new List<Mode>();
        public List<GameObject> resetPhysbones = new List<GameObject>();
        
        [Serializable]
        public class Mode {
            public State state;
            public Mode() {
                this.state = new State();
            }
            public Mode(State state) {
                this.state = state;
            }
            
            public bool ResetMePlease;
        }
    }

    [Serializable]
    public class Toggle : NewFeatureModel {
        public string name;
        public State state;
        public bool saved;
        public bool slider;
        public bool securityEnabled;
        public bool defaultOn;
        public bool includeInRest;
        public bool exclusiveOffState;
        public bool enableExclusiveTag;
        public string exclusiveTag;
        public List<GameObject> resetPhysbones = new List<GameObject>();
        [NonSerialized] public bool addMenuItem = true;
        [NonSerialized] public bool usePrefixOnParam = true;
    }

    [Serializable]
    public class Puppet : NewFeatureModel {
        public string name;
        public bool saved;
        public bool slider;
        public List<Stop> stops = new List<Stop>();
        
        [Serializable]
        public class Stop {
            public float x;
            public float y;
            public State state;
            public Stop(float x, float y, State state) {
                this.x = x;
                this.y = y;
                this.state = state;
            }
        }
    }

    [Serializable]
    public class SecurityLock : NewFeatureModel {
        public string pinNumber;
    }

    [Serializable]
    public class SenkyGestureDriver : NewFeatureModel {
        public State eyesClosed;
        public State eyesHappy;
        public State eyesSad;
        public State eyesAngry;

        public State mouthBlep;
        public State mouthSuck;
        public State mouthSad;
        public State mouthAngry;
        public State mouthHappy;

        public State earsBack;
        
        public float transitionTime = -1;
    }

    [Serializable]
    public class Talking : NewFeatureModel {
        public State state;
    }

    [Serializable]
    public class Toes : NewFeatureModel {
        public State down;
        public State up;
        public State splay;
    }

    [Serializable]
    public class Visemes : NewFeatureModel {
        public float transitionTime = -1;
        public State state_sil;
        public State state_PP;
        public State state_FF;
        public State state_TH;
        public State state_DD;
        public State state_kk;
        public State state_CH;
        public State state_SS;
        public State state_nn;
        public State state_RR;
        public State state_aa;
        public State state_E;
        public State state_I;
        public State state_O;
        public State state_U;
    }
    
    [Serializable]
    public class ArmatureLink : NewFeatureModel {
        public GameObject propBone;
        public HumanBodyBones boneOnAvatar;
        public string bonePathOnAvatar;
        public bool useOptimizedUpload;
        public bool keepBoneOffsets;
        public bool useBoneMerging;
        public string removeBoneSuffix;
    }
    
    [Serializable]
    public class TPSIntegration : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new TPSIntegration2();
        }
    }
    
    [Serializable]
    public class TPSIntegration2 : NewFeatureModel {
    }
    
    [Serializable]
    public class BoundingBoxFix : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new BoundingBoxFix2();
        }
    }
    
    [Serializable]
    public class BoundingBoxFix2 : NewFeatureModel {
    }

    [Serializable]
    public class BoneConstraint : NewFeatureModel {
        public GameObject obj;
        public HumanBodyBones bone;
    }
    
    [Serializable]
    public class MakeWriteDefaultsOff : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new MakeWriteDefaultsOff2();
        }
    }
    
    [Serializable]
    public class MakeWriteDefaultsOff2 : NewFeatureModel {
    }
    
    [Serializable]
    public class OGBIntegration : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new OGBIntegration2();
        }
    }
    
    [Serializable]
    public class OGBIntegration2 : NewFeatureModel {
    }
    
    [Serializable]
    public class RemoveHandGestures : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new RemoveHandGestures2();
        }
    }
        
    [Serializable]
    public class RemoveHandGestures2 : NewFeatureModel {
    }

    [Serializable]
    public class CrossEyeFix : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new CrossEyeFix2();
        }
    }
    
    [Serializable]
    public class CrossEyeFix2 : NewFeatureModel {
    }
    
    [Serializable]
    public class AnchorOverrideFix : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new AnchorOverrideFix2();
        }
    }
    
    [Serializable]
    public class AnchorOverrideFix2 : NewFeatureModel {
    }
    
    [Serializable]
    public class MoveMenuItem : NewFeatureModel {
        public string fromPath;
        public string toPath;
    }
    
    [Serializable]
    public class GestureDriver : NewFeatureModel {
        public List<Gesture> gestures = new List<Gesture>();
        
        [Serializable]
        public class Gesture {
            public Hand hand;
            public HandSign sign;
            public HandSign comboSign;
            public State state;
            public bool disableBlinking;
            public bool customTransitionTime;
            public float transitionTime = 0;
            public bool enableLockMenuItem;
            public string lockMenuItem;
            public bool enableExclusiveTag;
            public string exclusiveTag;
            public bool enableWeight;
            
            public bool ResetMePlease;
        }

        public enum Hand {
            EITHER,
            LEFT,
            RIGHT,
            COMBO
        }
        
        public enum HandSign {
            NEUTRAL,
            FIST,
            HANDOPEN,
            FINGERPOINT,
            VICTORY,
            ROCKNROLL,
            HANDGUN,
            THUMBSUP
        }
    }
    
    [Serializable]
    public class Gizmo : NewFeatureModel {
        public Vector3 rotation;
        public string text;
        public float sphereRadius;
        public float arrowLength;
    }
    
    [Serializable]
    public class ObjectState : NewFeatureModel {
        public List<ObjState> states = new List<ObjState>();

        [Serializable]
        public class ObjState {
            public GameObject obj;
            public Action action = Action.DEACTIVATE;
            public bool ResetMePlease;
        }
        public enum Action {
            DEACTIVATE,
            ACTIVATE,
            DELETE
        }
    }

    [Serializable]
    public class BlendShapeLink : NewFeatureModel {
        public List<GameObject> objs;
        public string baseObj;
    }

}
