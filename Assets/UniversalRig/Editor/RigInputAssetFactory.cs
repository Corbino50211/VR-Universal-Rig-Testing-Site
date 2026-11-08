using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UniversalRig.Editor
{
    public static class RigInputAssetFactory
    {
        public static InputActionAsset Create(string path)
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "SoldierInput";
            InputActionMap map = asset.AddActionMap("UniversalRig");
            Pose(map, "Head", "<XRHMD>", "centerEyePosition", "centerEyeRotation");
            Pose(map, "Left", "<XRController>{LeftHand}", "devicePosition", "deviceRotation");
            Pose(map, "Right", "<XRController>{RightHand}", "devicePosition", "deviceRotation");
            map.AddAction("Move", InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis", expectedControlLayout: "Vector2");
            map.AddAction("Turn", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis", expectedControlLayout: "Vector2");
            map.AddAction("LeftGrip", InputActionType.Value, "<XRController>{LeftHand}/grip", expectedControlLayout: "Axis");
            map.AddAction("RightGrip", InputActionType.Value, "<XRController>{RightHand}/grip", expectedControlLayout: "Axis");
            map.AddAction("LeftTrigger", InputActionType.Value, "<XRController>{LeftHand}/trigger", expectedControlLayout: "Axis");
            map.AddAction("RightTrigger", InputActionType.Value, "<XRController>{RightHand}/trigger", expectedControlLayout: "Axis");
            map.AddAction("Jump", InputActionType.Button, "<XRController>{RightHand}/primaryButton", expectedControlLayout: "Button");
            map.AddAction("Calibrate", InputActionType.Button, "<XRController>{LeftHand}/primaryButton", expectedControlLayout: "Button");
            File.WriteAllText(path, asset.ToJson());
            UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        private static void Pose(InputActionMap map, string prefix, string device, string position, string rotation)
        {
            map.AddAction(prefix + "Position", InputActionType.Value, device + "/" + position, expectedControlLayout: "Vector3");
            map.AddAction(prefix + "Rotation", InputActionType.Value, device + "/" + rotation, expectedControlLayout: "Quaternion");
            map.AddAction(prefix + "Tracked", InputActionType.Value, device + "/isTracked", expectedControlLayout: "Button");
            map.AddAction(prefix + "State", InputActionType.Value, device + "/trackingState", expectedControlLayout: "Integer");
        }
    }
}
