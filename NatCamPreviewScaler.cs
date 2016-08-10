/* 
*   NatCam
*   Copyright (c) 2016 Yusuf Olokoba
*/

#if UNITY_5_3_5 || UNITY_5_4_OR_NEWER //5.3.5
#define VERTEX_HELPER
#endif

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using NatCamU.Internals;
using Ext = NatCamU.Internals.NatCamExtensions;

namespace NatCamU {
    
    [RequireComponent(typeof(RectTransform))] [NCDoc(19)]
    public class NatCamPreviewScaler : NatCamPreviewBehaviour, IMeshModifier {
        
        [Tooltip(positionTip)] [NCDoc(67)] public bool positionAtScreenCentre = true;
        [Tooltip(scaleTip)] [NCDoc(68)] [NCRef(0)] public ScaleMode scaleMode = ScaleMode.None;
        
        private Vector2? dim = null;
        private Canvas canvas;
        private Graphic graphic {get {_graphic = _graphic ?? GetComponent<Graphic>(); return _graphic;}}
        private RectTransform rectTransform {get {_rectTransform = _rectTransform ?? GetComponent<RectTransform>(); return _rectTransform;}}
        private Graphic _graphic;
        private RectTransform _rectTransform;


        #region --Ops--

        protected override void Apply () {
            //Apply
            Apply(Regularize(NatCam.Preview));
        }

        ///<summary>
        ///Scale the UI Panel to conform to a texture.
        ///</summary>
        [NCDoc(137)] [NCCode(3)]
        public void Apply (Texture texture) {
            //Apply
            Apply(new Vector2(texture.width, texture.height));
        }
        
        ///<summary>
        ///Scale the UI Panel to conform to a custom size.
        ///</summary>
        [NCDoc(138)]
        public void Apply (Vector2 customResolution) {
            //Null checking
            if (graphic == null) {
                Ext.Warn("Scaler Apply failed, there is no graphic attached to the gameObject");
                return;
            }
            //Apply
            InternalApply(customResolution).Invoke(this);
        }

        private IEnumerator InternalApply (Vector2 customResolution) {
            //Unity iOS bug :/
            while (Screen.orientation == ScreenOrientation.Unknown) yield return null;
            //Get the canvas
            canvas = graphic.canvas;
            //Apply
            dim = customResolution;
            //Positioning term
            if (positionAtScreenCentre) rectTransform.position = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, canvas.planeDistance));
            //Dirty
            if (graphic) graphic.SetAllDirty();
        }
        #endregion


        #region --Unity UI Callbacks--
        
        public void ModifyMesh (VertexHelper helper) {
            #if VERTEX_HELPER
            Vector3[] verts = CalculateVertices();
            UIVertex[] quad = new UIVertex[4];
            UIVertex vert = UIVertex.simpleVert;
            //Get the color
            Color color = graphic == null ? Color.white : graphic.color;
            //Vert0
            vert.position = verts[0];
            vert.uv0 = new Vector2(0f, 0f);
            vert.color = color;
            quad[0] = vert;
            //Vert1
            vert.position = verts[1];
            vert.uv0 = new Vector2(0, 1);
            vert.color = color;
            quad[1] = vert;
            //Vert2
            vert.position = verts[2];
            vert.uv0 = new Vector2(1, 1);
            vert.color = color;
            quad[2] = vert;
            //Vert3
            vert.position = verts[3];
            vert.uv0 = new Vector2(1, 0f);
            vert.color = color;
            quad[3] = vert;
            //Helper
            helper.Clear();
            helper.AddUIVertexQuad(quad);
            #endif
        }

        public void ModifyMesh (Mesh mesh) {
            #if !VERTEX_HELPER
            var list = new System.Collections.Generic.List<Vector3>(CalculateVertices());
            mesh.SetVertices(list);
            #endif
        }
        #endregion
        

        #region --Panel Scaling--

        Vector3[] CalculateVertices () {
            Vector2 corner1 = Vector2.zero;
            Vector2 corner2 = Vector2.one;
            float width, height;
            CalculateExtents(out width, out height);
            //Scale
            corner1.x *= width;
            corner1.y *= height;
            corner2.x *= width;
            corner2.y *= height;
            //Create a pivot vector, and pivot compensation vector
            Vector3 piv = new Vector3(rectTransform.pivot.x, rectTransform.pivot.y, 0f), comp = new Vector3(piv.x * width, piv.y * height, 0f);
            Vector3[] verts = new [] {
                new Vector3(corner1.x, corner1.y, 0f) - comp,
                new Vector3(corner1.x, corner2.y, 0f) - comp,
                new Vector3(corner2.x, corner2.y, 0f) - comp,
                new Vector3(corner2.x, corner1.y, 0f) - comp
            };
            return verts;
        }

        void CalculateExtents (out float width, out float height) { //INCOMPLETE //Scale factor fixed x variable y
            width = height = 0;
            Vector2 principal = dim == null ? new Vector2(rectTransform.rect.width, rectTransform.rect.height) : dim.Value;
            float aspect = principal.x / principal.y;
            if (graphic == null) return;
            else if (canvas == null) canvas = graphic.canvas;
            switch (scaleMode) {
                case ScaleMode.FixedWidthVariableHeight :
                    width = rectTransform.rect.width;
                    height = width / aspect;
                break;
                case ScaleMode.FixedHeightVariableWidth :
                    height = rectTransform.rect.height;
                    width = height * aspect;
                break;
                case ScaleMode.FillScreen :
                    float scale = Mathf.Max(canvas.pixelRect.width / principal.x, canvas.pixelRect.height / principal.y) / canvas.scaleFactor;
                    width = scale * principal.x;
                    height = scale * principal.y;
                break;
                case ScaleMode.None :
                    width = rectTransform.rect.width;
                    height = rectTransform.rect.height;
                break;
            }
        }

        Vector2 Regularize (Texture tex) {
            Vector2 input = new Vector2(tex.width, tex.height);
            bool isPortrait = 
                Screen.orientation == ScreenOrientation.Portrait || 
                Screen.orientation == ScreenOrientation.PortraitUpsideDown || 
                (Screen.orientation == ScreenOrientation.AutoRotation && 
                (Input.deviceOrientation == DeviceOrientation.Portrait || 
                Input.deviceOrientation == DeviceOrientation.PortraitUpsideDown)); //This is the only appropriate flag
            int min = Mathf.RoundToInt(Mathf.Min(input.x, input.y)), max = Mathf.RoundToInt(Mathf.Max(input.x, input.y));
            Ext.LogVerbose("PreviewScaler: orientation-"+Screen.orientation+" portrait-"+isPortrait+" min-"+min+" max-"+max);
            return new Vector2 {
                x = isPortrait ? min : max,
                y = isPortrait ? max : min
            };
        }
        #endregion

        private const string
        positionTip = "This positions the UI Panel at the centre of the screen. Note that this works best when the pivot is centered.",
        scaleTip = "This dictates how NatCam applies scaling considering the active camera's preview resolution.";
    }
}