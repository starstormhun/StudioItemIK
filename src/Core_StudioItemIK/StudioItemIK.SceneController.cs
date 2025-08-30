using System.Collections.Generic;
using KKAPI.Studio.SaveLoad;
using ExtensibleSaveFormat;
using KKAPI.Utilities;
using System.Linq;
using MessagePack;
using Studio;

namespace StudioItemIK
{
    class SceneController : SceneCustomFunctionController
    {

        protected override void OnSceneSave()
        {
            PluginData data = new PluginData();

            Dictionary<int, ObjectCtrlInfo> idObjectPairs = Studio.Studio.Instance.dicObjectCtrl;

            Dictionary<int, byte[]> activeIKsSerialised = new Dictionary<int, byte[]>();
            Dictionary<int, List<int>> objectTargetPairsSerialised = new Dictionary<int, List<int>>();
            foreach(OCIItem item in StudioItemIK.activeIKs.Keys)
            {
                int id = item.objectInfo.dicKey;

                byte[] IKData = StudioItemIK.activeIKs[item].serialiseIKData();
                activeIKsSerialised[id] = IKData;

                List<int> targetsAndPoles = new List<int>();
                foreach(OCIItem item2 in StudioItemIK.objectTargetPairs[item])
                {
                    targetsAndPoles.Add(item2.objectInfo.dicKey);
                }
                objectTargetPairsSerialised[id] = targetsAndPoles;
            }

            data.data.Add("activeIKs", MessagePackSerializer.Serialize<Dictionary<int, byte[]>>(activeIKsSerialised));
            data.data.Add("objectTargetPairs", MessagePackSerializer.Serialize<Dictionary<int, List<int>>>(objectTargetPairsSerialised));
            SetExtendedData(data);

        }
        protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            var data = GetExtendedData();
            if (operation == SceneOperationKind.Clear || operation == SceneOperationKind.Load)
            {
                StudioItemIK.objectTargetPairs.Clear();
                StudioItemIK.setGuiContentNone();
            }
            if (data == null) return;
            if (operation == SceneOperationKind.Clear) return;

            Dictionary<int, byte[]> activeIKsSerialised = new Dictionary<int, byte[]>();
            Dictionary<int, List<int>> objectTargetPairsSerialised = new Dictionary<int, List<int>>();

            if (data.data.TryGetValue("activeIKs", out var activeIKsSerialised_) && activeIKsSerialised_ != null)
            {
                activeIKsSerialised = MessagePackSerializer.Deserialize<Dictionary<int, byte[]>>((byte[])activeIKsSerialised_);
            }

            if (data.data.TryGetValue("objectTargetPairs", out var objectTargetPairsSerialised_) && objectTargetPairsSerialised_ != null)
            {
                objectTargetPairsSerialised = MessagePackSerializer.Deserialize<Dictionary<int, List<int>>>((byte[])objectTargetPairsSerialised_);
            }


            foreach (int id in objectTargetPairsSerialised.Keys)
            {

                OCIItem item = (OCIItem)loadedItems[id];
                List<OCIItem> targetAndPoles = new List<OCIItem>();
                foreach(int id2 in objectTargetPairsSerialised[id])
                {
                    targetAndPoles.Add((OCIItem)loadedItems[id2]);
                }
                byte[] IKDataSerialised = activeIKsSerialised[id];
                IKData ikdata = MessagePackSerializer.Deserialize<IKData>(IKDataSerialised);
                int chainLegnth = ikdata.ChainLength;
                int poleAmount = ikdata.poleAmount;

                FabrikIK IK = StudioItemIK.reenableFabrikIK(item, chainLegnth, poleAmount, targetAndPoles);
                IK.setIKData(ikdata);
            }
        }

        protected override void OnObjectsCopied(ReadOnlyDictionary<int, ObjectCtrlInfo> copiedItems)
        {
            Dictionary<int, ObjectCtrlInfo> sceneObjects = Studio.Studio.Instance.dicObjectCtrl;
            foreach (int id in copiedItems.Keys)
            {
                if (copiedItems[id] is OCIItem)
                {
                    OCIItem newItem = (OCIItem)copiedItems[id];
                    OCIItem oldItem = (OCIItem)sceneObjects[id];
                    if (StudioItemIK.activeIKs.Keys.Contains(oldItem))
                    {
                        FabrikIK oldIK = StudioItemIK.activeIKs[oldItem];
                        List<OCIItem> targetAndPoles = new List<OCIItem>();
                        foreach (OCIItem item in StudioItemIK.objectTargetPairs[oldItem])
                        {
                            targetAndPoles.Add((OCIItem)copiedItems[item.objectInfo.dicKey]);
                        }
                        FabrikIK newIK = StudioItemIK.reenableFabrikIK(newItem, oldIK.ChainLength, oldIK.Poles.Count, targetAndPoles);
                        newIK.setIKData(oldIK.getIKData());
                    }
                }
            }
        }
    }
}
