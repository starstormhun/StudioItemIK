using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKAPI;
using Studio;

namespace StudioItemIk
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [BepInProcess("CharaStudio")]
    public class StudioItemIk : BaseUnityPlugin
    {
        public const string PluginName = "KKS_StudioItemIK";
        public const string GUID = "org.njaecha.plugins.StudioItemIK";
        public const string Version = "0.1.0";

        internal static bool draw = true;
        private bool oldDraw = true;
        private bool ui = false;
        private ConfigEntry<KeyboardShortcut> hotkey;
        private Rect windowRect = new Rect(500, 100, 200, 205);


        internal new static ManualLogSource Logger;
        internal static Studio.CameraControl camCtrl;
        internal static Studio.TreeNodeCtrl treeNodeCtrl;
        private Vector3 oldCamPosition;
        private Quaternion oldCamRotation;
        private TreeNodeObject lastSelectedItem = null;
        private int lastSelectedItemChainLength;
        private int lastSelectedItemPoleAmount;

        internal static List<FabrikIK> activeIKs = new List<FabrikIK>();
        private Dictionary<FabrikIK, List<OCIItem>> IKTargetPairs = new Dictionary<FabrikIK, List<OCIItem>>();

        // ui content
        private string currentItemName = "";
        private enum guiState
        {
            IK,
            notItem,
            noBones,
            noObject
        }
        private guiState uiState = guiState.noObject;
        private int currentItemChainLength = 0;
        private int currentItemPoleAmount = 0;
        private bool currentItemHasIK = false;

        void Awake()
        {
            StudioItemIk.Logger = base.Logger;
            KeyboardShortcut defaultShortcut = new KeyboardShortcut(KeyCode.I, KeyCode.LeftShift);
            hotkey = Config.Bind("StudioItemIK", "Hotkey", defaultShortcut, "Press this key to open UI");
            KKAPI.Studio.StudioAPI.StudioLoadedChanged += registerCtrls;
        }
        void OnGUI()
        {
            if (ui)
            {
                windowRect = GUI.Window(5931, windowRect, WindowFunction, $"StudioItemIK v{Version}");
                KKAPI.Utilities.IMGUIUtils.EatInputInRect(windowRect);
            }
        }
        private void registerCtrls(object sender, EventArgs e)
        {
            // camCtrl
            camCtrl = Singleton<Studio.Studio>.Instance.cameraCtrl;
            oldCamPosition = camCtrl.transform.position;
            oldCamRotation = camCtrl.transform.rotation;
            // treeNodeCtrl
            treeNodeCtrl = Singleton<Studio.Studio>.Instance.treeNodeCtrl;
            treeNodeCtrl.onSelect += setGuiContent;
            treeNodeCtrl.onDeselect += setGuiContentNone;
            treeNodeCtrl.onDelete += setGuiContentNone;
        }
        private void setGuiContent(TreeNodeObject tno)
        {
            currentItemName = tno.textName;
            ObjectCtrlInfo selectedObject = KKAPI.Studio.StudioAPI.GetSelectedObjects().First();
            if (selectedObject is OCIItem)
            {
                OCIItem item = (OCIItem)selectedObject;
                if (item.listBones != null)
                {
                    if (item.listBones.Count > 3)
                    {
                        // test if item already has IK enabled
                        FabrikIK IK = item.listBones[item.listBones.Count - 1].guideObject.transformTarget.GetComponent<FabrikIK>();
                        if (tno == lastSelectedItem)
                        {
                            currentItemChainLength = lastSelectedItemChainLength;
                            currentItemPoleAmount = lastSelectedItemPoleAmount;
                        }
                        else
                        {
                            if (IK != null)
                            {
                                currentItemChainLength = IK.ChainLength;
                                currentItemPoleAmount = IK.Poles.Count;
                            }
                            else
                            {
                                currentItemChainLength = item.listBones.Count;
                                currentItemPoleAmount = 0;
                            }
                        }
                        currentItemHasIK = IK != null ? true : false;

                        uiState = guiState.IK;

                        lastSelectedItem = tno;
                        lastSelectedItemChainLength = currentItemChainLength;
                        lastSelectedItemPoleAmount = currentItemPoleAmount;
                    }
                    else uiState = guiState.noBones;
                }
                else uiState = guiState.noBones;
            }
            else uiState = guiState.notItem;
        }
        private void setGuiContentNone(TreeNodeObject tno)
        {
            uiState = guiState.noObject;
        }
        private void WindowFunction(int WindowID)
        {
            GUIStyle textCentered = new GUIStyle();
            textCentered.alignment = TextAnchor.MiddleCenter;
            textCentered.wordWrap = true;
            textCentered.normal.textColor = Color.white;
            switch (uiState)
            {
                case guiState.noObject:
                    uiNoObject();
                    break;
                case guiState.notItem:
                    uiNotIem();
                    break;
                case guiState.noBones:
                    uiNoBones();
                    break;
                case guiState.IK:
                    uiIK();
                    break;
                default:
                    uiNoObject();
                    break;
            }
            void uiNoObject()
            {
                GUI.Label(new Rect(0, 0, windowRect.width, windowRect.height), "Please select an object!", textCentered);
            }
            void uiNotIem()
            {
                GUI.Label(new Rect(0, 0, windowRect.width, windowRect.height), "Selected object is not a\nstudio item!", textCentered);
            }
            void uiNoBones()
            {
                GUI.Label(new Rect(0, 0, windowRect.width, windowRect.height), "Selected studio item has\nno or too little bones!", textCentered);
            }
            void uiIK()
            {
                OCIItem selectedObject = (OCIItem)KKAPI.Studio.StudioAPI.GetSelectedObjects().First();

                GUI.Label(new Rect(10, 20, 180, 30), currentItemName, textCentered);

                GUI.Label(new Rect(10, 60, 100, 20), $"Chain Legth: {currentItemChainLength}");
                if (GUI.Button(new Rect(130, 60, 20, 20), "+"))
                {
                    if (currentItemChainLength != selectedObject.listBones.Count)
                    {
                        currentItemChainLength++;
                        lastSelectedItemChainLength++;
                        if (currentItemHasIK)
                            updateFabrikIK(selectedObject, currentItemChainLength, currentItemPoleAmount);
                    }
                }
                if (GUI.Button(new Rect(160, 60, 20, 20), "-"))
                {
                    if (currentItemChainLength > 2)
                    {
                        currentItemChainLength--;
                        lastSelectedItemChainLength--;
                        if (currentItemHasIK)
                            updateFabrikIK(selectedObject, currentItemChainLength, currentItemPoleAmount);
                    }
                }

                GUI.Label(new Rect(10, 90, 100, 20), $"Pole Amount: {currentItemPoleAmount}");
                if (GUI.Button(new Rect(130, 90, 20, 20), "+"))
                {
                    if (currentItemPoleAmount != (currentItemChainLength - 1))
                    {
                        currentItemPoleAmount++;
                        lastSelectedItemPoleAmount++;
                        if (currentItemHasIK)
                            updateFabrikIK(selectedObject, currentItemChainLength, currentItemPoleAmount);
                    }
                }
                if (GUI.Button(new Rect(160, 90, 20, 20), "-"))
                {
                    if (currentItemPoleAmount > 0)
                    {
                        currentItemPoleAmount--;
                        lastSelectedItemPoleAmount--;
                        if (currentItemHasIK)
                            updateFabrikIK(selectedObject, currentItemChainLength, currentItemPoleAmount);
                    }
                }
                if (!currentItemHasIK)
                {
                    if (GUI.Button(new Rect(10, 130, 180, 40), "Activate IK"))
                    {
                        enableFabrikIK(selectedObject, currentItemChainLength, currentItemPoleAmount);
                        treeNodeCtrl.RefreshHierachy();
                        setGuiContent(treeNodeCtrl.selectNode);
                    }
                }
                else
                {
                    if (GUI.Button(new Rect(10, 130, 180, 40), "Remove IK"))
                    {
                        removeFabrikIK(selectedObject);
                        treeNodeCtrl.RefreshHierachy();
                        setGuiContent(treeNodeCtrl.selectNode);
                        currentItemHasIK = false;
                    }
                }
            }

            draw = GUI.Toggle(new Rect(10, 180, 180, 20), draw, "draw gizmos");
            if (oldDraw != draw)
            {
                oldDraw = draw;
                if (!draw)
                    if (activeIKs.Count > 0)
                        foreach (FabrikIK IK in activeIKs)
                        {
                            IK.setGizmo();
                            IK.drawGizmo();
                        }
                    else
                    if (activeIKs.Count > 0)
                        foreach (FabrikIK IK in activeIKs)
                        {
                            IK.DestroyGizmo();
                        }
                foreach (FabrikIK IK in activeIKs)
                {
                    IK.drawGizmo();
                }
            }

            GUI.DragWindow();
        }

        public void removeFabrikIK(OCIItem oci)
        {
            if (oci.listBones.Count < 3)
                return;
            FabrikIK IK = oci.listBones[oci.listBones.Count - 1].guideObject.transformTarget.GetComponent<FabrikIK>();
            if (IK != null)
            {
                oci.guideObject.changeAmount.onChangePos -= IK.setGizmo;
                oci.guideObject.changeAmount.onChangeRot -= IK.setGizmo;
                oci.guideObject.changeAmount.onChangeScale -= delegate { IK.setGizmo(); };
                oci.guideObject.changeAmount.onChangePos -= IK.ResolveIK;
                oci.guideObject.changeAmount.onChangeRot -= IK.ResolveIK;
                oci.guideObject.changeAmount.onChangeScale -= delegate { IK.ResolveIK(); };
                foreach (OCIItem oci2 in IKTargetPairs[IK])
                {
                    oci2.treeNodeObject.enableDelete = true;
                    treeNodeCtrl.DeleteNode(oci2.treeNodeObject);
                }
                activeIKs.Remove(IK);
                IKTargetPairs.Remove(IK);
                Destroy(IK);
            }
        }
        public void updateFabrikIK(OCIItem oci, int chainLength, int amountPoles)
        {
            FabrikIK IK = oci.listBones[oci.listBones.Count - 1].guideObject.transformTarget.GetComponent<FabrikIK>();
            if (IK != null)
            {
                if (amountPoles == IK.Poles.Count)
                {
                    IK.ChainLength = chainLength;
                    IK.Init();
                }
                else if (amountPoles > IK.Poles.Count)
                {
                    IK.ChainLength = chainLength;
                    int newPoleAmount = amountPoles - IK.Poles.Count;
                    for (int i = IK.Poles.Count; i < amountPoles; i++)
                    {
                        OCIItem pole = Studio.AddObjectItem.Load(new OIItemInfo(1, 1, 1, Studio.Studio.GetNewIndex()), oci, new TreeNodeObject());
                        //pole.guideObject.transformTarget.position = oci.listBones[(int)(oci.listBones.Count / (amountPoles + 1)) * i].posision;
                        pole.treeNodeObject.textName = $"{oci.treeNodeObject.textName}_Pole" + (i + 1);
                        pole.guideObject.changeAmount.scale = new Vector3(0.5f, 0.5f, 0.5f);
                        pole.guideObject.changeAmount.onChangePos += IK.ResolveIK;
                        pole.treeNodeObject.enableDelete = false;
                        pole.treeNodeObject.enableCopy = false;
                        IK.Poles.Add(pole.guideObject.transformTarget);
                        IKTargetPairs[IK].Add(pole);
                    }
                    for (int i = 1; i < IKTargetPairs[IK].Count; i++)
                    {
                        IKTargetPairs[IK][i].guideObject.transformTarget.position = oci.listBones[(int)(oci.listBones.Count / (amountPoles + 1)) * i].posision;
                    }
                    IK.Init();
                    treeNodeCtrl.RefreshHierachy();
                    setGuiContent(oci.treeNodeObject);
                }
                else
                {
                    IK.ChainLength = chainLength;
                    for (int i = IK.Poles.Count; i > amountPoles; i--)
                    {
                        OCIItem pole = IKTargetPairs[IK][IKTargetPairs[IK].Count - 1];
                        IK.Poles.Remove(pole.guideObject.transformTarget);
                        pole.treeNodeObject.enableDelete = true;
                        treeNodeCtrl.DeleteNode(pole.treeNodeObject);
                        IKTargetPairs[IK].Remove(pole);
                    }
                    for (int i = 1; i < IKTargetPairs[IK].Count; i++)
                    {
                        IKTargetPairs[IK][i].guideObject.transformTarget.position = oci.listBones[(int)(oci.listBones.Count / (amountPoles + 1)) * i].posision;
                    }
                    IK.Init();
                    treeNodeCtrl.RefreshHierachy();
                    setGuiContent(oci.treeNodeObject);
                }
                setGuiContent(treeNodeCtrl.selectNode);
            }
        }

        public void enableFabrikIK(OCIItem oci, int chainLength, int amountPoles = 0)
        {
            if (oci.listBones.Count < 3)
            {
                Logger.LogError("Item has too little bones");
                return;
            }
            if (amountPoles > oci.listBones.Count - 2)
            {
                Logger.LogError($"More poles and mid bones. Maximum: {oci.listBones.Count - 2}");
                return;
            }

            List<OCIItem> targetAndPoles = new List<OCIItem>();

            Transform leafBone = oci.listBones[oci.listBones.Count - 1].guideObject.transformTarget;    // get leafbone
            FabrikIK IK = leafBone.GetOrAddComponent<FabrikIK>();
            oci.guideObject.changeAmount.onChangePos += IK.setGizmo;
            oci.guideObject.changeAmount.onChangeRot += IK.setGizmo;
            oci.guideObject.changeAmount.onChangeScale += delegate { IK.setGizmo(); };
            oci.guideObject.changeAmount.onChangePos += IK.ResolveIK;
            oci.guideObject.changeAmount.onChangeRot += IK.ResolveIK;
            oci.guideObject.changeAmount.onChangeScale += delegate { IK.ResolveIK(); };


            // create target object
            OCIItem target = Studio.AddObjectItem.Load(new OIItemInfo(1, 1, 1, Studio.Studio.GetNewIndex()), oci, new TreeNodeObject());
            target.guideObject.transformTarget.position = leafBone.position;
            target.treeNodeObject.textName = $"{oci.treeNodeObject.textName}_Target";
            target.guideObject.changeAmount.scale = new Vector3(0.5f, 0.5f, 0.5f);
            target.guideObject.changeAmount.onChangePos += IK.ResolveIK;
            target.guideObject.changeAmount.onChangeRot += IK.ResolveIK;
            target.guideObject.scaleSelect = 0.05f;
            target.treeNodeObject.enableDelete = false;
            target.treeNodeObject.enableCopy = false;
            targetAndPoles.Add(target);

            // create pole objects
            if (amountPoles > 0)
            {
                List<Transform> poles = new List<Transform>();
                for (int i = 1; i <= amountPoles; i++)
                {
                    OCIItem pole = Studio.AddObjectItem.Load(new OIItemInfo(1, 1, 1, Studio.Studio.GetNewIndex()), oci, new TreeNodeObject());
                    pole.guideObject.transformTarget.position = oci.listBones[(int)(oci.listBones.Count / (amountPoles + 1)) * i].posision;
                    pole.treeNodeObject.textName = $"{oci.treeNodeObject.textName}_Pole" + i;
                    pole.guideObject.changeAmount.scale = new Vector3(0.5f, 0.5f, 0.5f);
                    pole.guideObject.changeAmount.onChangePos += IK.ResolveIK;
                    pole.treeNodeObject.enableDelete = false;
                    pole.treeNodeObject.enableCopy = false;
                    poles.Add(pole.guideObject.transformTarget);
                    targetAndPoles.Add(pole);
                }
                IK.Poles = poles;
            }

            // initalize IK
            IK.Target = target.guideObject.transformTarget;
            IK.ChainLength = chainLength;
            IK.hashString = oci.GetHashCode().ToString();
            IK.Init();
            activeIKs.Add(IK);
            IKTargetPairs[IK] = targetAndPoles;

        }
        void Update()
        {
            if (hotkey.Value.IsDown())
                ui = !ui;
            if (!draw)
                return;
            if (activeIKs.Count == 0)
                return;
            if (camCtrl.transform.position != oldCamPosition || camCtrl.transform.rotation != oldCamRotation)
            {
                oldCamPosition = camCtrl.transform.position;
                oldCamRotation = camCtrl.transform.rotation;
                foreach (FabrikIK IK in activeIKs)
                {
                    IK.drawGizmo();
                }
            }
        }
    }
}
