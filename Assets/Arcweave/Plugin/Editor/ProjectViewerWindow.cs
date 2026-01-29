#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Arcweave.Project;

namespace Arcweave
{
    public class ProjectViewerWindow : EditorWindow
    {
        private static readonly Dictionary<string, Color> COLOR_THEMES = new()
        {
            { "default", new Color(0.25f, 0.25f, 0.25f) },
            { "orange", new Color(0.52f, 0.19f, 0f) },
            { "brown", new Color(0.33f, 0.20f, 0f) },
            { "gold", new Color(0.40f, 0.39f, 0f) },
            { "moss", new Color(0.26f, 0.46f, 0f) },
            { "green", new Color(0f, 0.31f, 0.04f) },

            { "cyan", new Color(0f, 0.44f, 0.38f) },
            { "lightBlue", new Color(0f, 0.36f, 0.59f) },
            { "purple", new Color(0.05f, 0.17f, 0.65f) },
            { "blue", new Color(0.05f, 0.17f, 0.65f) },
            { "pink", new Color(0.44f, 0f, 0.30f) },
            { "red", new Color(0.41f, 0.09f, 0f) },
        };

        private readonly float ZOOM_MIN = 0.1f;
        private readonly float ZOOM_MAX = 2f;
        private readonly int TOP_MARGIN = 21;
        private readonly int BOTTOM_MARGIN = 5;
        private readonly int SIDE_MARGIN = 5;
        private readonly int GRID_SIZE = 20;
        private readonly int MIN_NODE_WIDTH = 200;
        private readonly int COVER_HEIGHT = 100;
        private readonly int COMPONENT_ICON_SIZE = 40;
        private readonly Color NODE_BASE_COLOR = new Color(0.07f, 0.07f, 0.07f);
        private readonly Color NODE_DEFAULT_COLOR = COLOR_THEMES["default"];

        private ArcweaveProjectAsset _asset;
        private int _assetID;
        private Rect _canvasRect;
        private Vector2 _translation;
        private float _zoomFactor = 1f;
        private int _currentBoardIndex;
        private GUIStyle _contentStyle;
        private GUIStyle _labelStyle;

        private Dictionary<string, Rect> _nodeRects;

        ///----------------------------------------------------------------------------------------------

        private ArcweaveProjectAsset asset {
            get
            {
                if ( _asset == null ) {
                    _asset = EditorUtility.InstanceIDToObject(_assetID) as ArcweaveProjectAsset;
                }
                return _asset;
            }
            set
            {
                _asset = value;
                _assetID = value != null ? value.GetInstanceID() : 0;
            }
        }

        private Project.Project project => asset?.Project;

        ///----------------------------------------------------------------------------------------------

        //...
        private Vector2 pan {
            get { return _translation; }
            set { _translation = value; }
        }

        //...
        private float zoomFactor {
            get { return Mathf.Clamp(_zoomFactor, ZOOM_MIN, ZOOM_MAX); }
            set { _zoomFactor = Mathf.Clamp(value, ZOOM_MIN, ZOOM_MAX); }
        }

        ///----------------------------------------------------------------------------------------------

        //...
        public static void Open(ArcweaveProjectAsset asset) {
            var window = GetWindow<ProjectViewerWindow>();
            window._asset = asset;
            window._assetID = asset.GetInstanceID();
            window._currentBoardIndex = 0;
            window.PanTo(asset.Project.Boards[0].Nodes[0].Pos - new Vector2(100, 100));
        }

        ///----------------------------------------------------------------------------------------------

        //...
        void OnEnable() {
            titleContent = new GUIContent("Arcweave Viewer");
            _nodeRects = new Dictionary<string, Rect>();
            _contentStyle = new GUIStyle();
            _contentStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
            _contentStyle.richText = true;
            _contentStyle.wordWrap = true;
            _contentStyle.padding = new RectOffset(15, 15, 10, 10);
            _contentStyle.clipping = TextClipping.Clip;
            _contentStyle.alignment = TextAnchor.UpperLeft;
            _contentStyle.fontSize = 12;

            _labelStyle = new GUIStyle();
            _labelStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
            _labelStyle.richText = true;
            _labelStyle.padding = new RectOffset(5, 5, 5, 5);
            _labelStyle.alignment = TextAnchor.MiddleCenter;
            _labelStyle.fontSize = 9;
        }

        //...
        void OnDisable() { }

        //...
        void OnGUI() {

            if ( asset == null ) {
                return;
            }

            _currentBoardIndex = Mathf.Clamp(_currentBoardIndex, 0, project.Boards.Count - 1);
            _canvasRect = Rect.MinMaxRect(SIDE_MARGIN, TOP_MARGIN, position.width - SIDE_MARGIN, position.height - BOTTOM_MARGIN);

            GUI.color = new Color(0.13f, 0.13f, 0.13f);
            GUI.DrawTexture(_canvasRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            DrawGrid(_canvasRect, pan, zoomFactor);
            HandleEvents();

            var originalCanvasRect = _canvasRect;
            var originalMatrix = default(Matrix4x4);
            if ( zoomFactor != 1 ) {
                _canvasRect = StartZoomArea(_canvasRect, zoomFactor, out originalMatrix);
            }

            GUI.BeginClip(_canvasRect, pan / zoomFactor, default(Vector2), false);
            DrawNodes();
            GUI.EndClip();

            if ( zoomFactor != 1 && originalMatrix != default(Matrix4x4) ) {
                EndZoomArea(originalMatrix);
                _canvasRect = originalCanvasRect;
            }

            ShowToolbar();

            GUI.color = Color.white;
            GUI.backgroundColor = Color.white;
        }

        ///----------------------------------------------------------------------------------------------

        //...
        void HandleEvents() {
            var e = Event.current;
            if ( e.type == EventType.ScrollWheel && _canvasRect.Contains(e.mousePosition) ) {
                ZoomAt(e.mousePosition, -e.delta.y > 0 ? 0.15f : -0.15f);
                e.Use();
            }

            if ( ( e.type == EventType.MouseDrag && _canvasRect.Contains(e.mousePosition) ) ) {
                pan += e.delta;
                e.Use();
            }
        }

        ///----------------------------------------------------------------------------------------------

        //...
        void ShowToolbar() {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Space(SIDE_MARGIN);
            GUILayout.Label(project.name + " ");
            if ( GUILayout.Button(project.Boards[_currentBoardIndex].Name, EditorStyles.toolbarDropDown, GUILayout.Width(200)) ) {
                var menu = new GenericMenu();
                for ( var i = 0; i < project.Boards.Count; i++ ) {
                    var _i = i;
                    var boardName = project.Boards[_i].Name;
                    menu.AddItem(new GUIContent(boardName), _i == _currentBoardIndex, () => _currentBoardIndex = _i);
                }
                menu.ShowAsContext();
            }
            GUILayout.FlexibleSpace();
            if ( GUILayout.Button("Re-Import", EditorStyles.toolbarButton) ) {
                asset.ImportProject();
            }
            GUILayout.Space(SIDE_MARGIN);
            GUILayout.EndHorizontal();
        }

        //...
        void DrawNodes() {

            System.Action delayDraw = null;

            foreach ( var node in project.Boards[_currentBoardIndex].Nodes ) {

                if ( node is Element ) {

                    var e = node as Element;
                    var coverImage = e.cover?.ResolveImage();

                    var title = string.Format("<size=14><b>{0}</b></size>", ( e.Id == project.StartingElement.Id ? "<color=#db841e>★</color> " : string.Empty ) + Interpreter.Utils.CleanString(e.Title));
                    var content = string.Format("<size=11>{0}</size>", Interpreter.Utils.CleanString(e.RawContent));

                    var titleSize = _contentStyle.CalcSize(new GUIContent(title));
                    titleSize.x = Mathf.Max(titleSize.x, MIN_NODE_WIDTH);
                    titleSize.x = Mathf.CeilToInt(titleSize.x / GRID_SIZE) * GRID_SIZE;

                    var rect = new Rect(node.Pos.x, node.Pos.y, titleSize.x, titleSize.y);

                    var titleRect = rect;

                    var coverRect = rect;
                    coverRect.y = titleRect.yMax + ( coverImage != null ? 5 : 0 );
                    coverRect.xMin += 5; coverRect.xMax -= 5;
                    coverRect.height = coverImage != null ? COVER_HEIGHT : 0;

                    var componentsRect = rect;
                    componentsRect.y = coverRect.yMax + ( e.Components.Count > 0 ? 5 : 0 );
                    componentsRect.xMin += 15; componentsRect.xMax -= 15;
                    var xCountFit = Mathf.FloorToInt(componentsRect.width / COMPONENT_ICON_SIZE);
                    var rowsCount = Mathf.CeilToInt(e.Components.Count / (float)xCountFit);
                    componentsRect.height = rowsCount * COMPONENT_ICON_SIZE;

                    var contentRect = rect;
                    contentRect.y = componentsRect.yMax;
                    contentRect.height = _contentStyle.CalcHeight(new GUIContent(content), rect.width);

                    rect.yMax = Mathf.CeilToInt(contentRect.yMax / GRID_SIZE) * GRID_SIZE;

                    _nodeRects[node.Id] = rect;

                    delayDraw += () =>
                    {
                        GUI.color = NODE_BASE_COLOR;
                        GUI.DrawTexture(rect, Texture2D.whiteTexture);
                        GUI.color = COLOR_THEMES[e.ColorTheme];
                        GUI.DrawTexture(titleRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        GUI.Label(titleRect, title, _contentStyle);
                        if ( coverImage != null ) { GUI.DrawTexture(coverRect, coverImage, ScaleMode.ScaleToFit); }
                        var row = -1;
                        var col = -1;
                        for ( var i = 0; i < e.Components.Count; i++ ) {
                            if ( i % xCountFit == 0 ) {
                                row++;
                                col = 0;
                            }
                            var cr = new Rect(componentsRect.x + ( col * COMPONENT_ICON_SIZE ), componentsRect.y + ( row * COMPONENT_ICON_SIZE ), COMPONENT_ICON_SIZE - 2, COMPONENT_ICON_SIZE - 2);
                            GUI.DrawTexture(cr, e.Components[i].cover?.ResolveImage(), ScaleMode.ScaleAndCrop);
                            col++;
                        }
                        GUI.Label(contentRect, content, _contentStyle);
                    };
                }

                if ( node is Branch ) {

                    var b = node as Branch;
                    string content = string.Empty;
                    for ( var i = 0; i < b.Conditions.Count; i++ ) {
                        var script = b.Conditions[i].Script;
                        script = string.IsNullOrEmpty(script) ? "..." : Interpreter.Utils.CleanString(script);
                        if ( i == 0 ) {
                            content += "<b><color=#eeeeee>if</color></b> " + script;
                        } else if ( i == b.Conditions.Count - 1 ) {
                            content += "\n\n<b><color=#eeeeee>else</color></b>";
                        } else {
                            content += "\n\n<b><color=#eeeeee>elseIf</color></b> " + script;
                        }
                    }

                    var size = _contentStyle.CalcSize(new GUIContent(content));
                    size.x = Mathf.Max(size.x, 200);

                    var rect = new Rect(node.Pos.x, node.Pos.y, size.x, size.y);
                    _nodeRects[node.Id] = rect;

                    delayDraw += () =>
                    {
                        GUI.color = NODE_BASE_COLOR;
                        GUI.DrawTexture(rect, Texture2D.whiteTexture);
                        GUI.color = COLOR_THEMES[b.colorTheme];
                        GUI.DrawTexture(new Rect(rect.x, rect.y, 10, rect.height), Texture2D.whiteTexture);
                        for ( var i = 1; i < b.Conditions.Count; i++ ) {
                            var sep = new Rect(rect.x, rect.y + 0 + ( ( rect.height / b.Conditions.Count ) * i ), rect.width, 1);
                            GUI.DrawTexture(sep, Texture2D.whiteTexture);
                        }
                        GUI.color = Color.white;
                        GUI.Label(rect, content, _contentStyle);
                    };
                }

                if ( node is Jumper ) {

                    var j = node as Jumper;
                    var content = string.Format("<size=14><b>↪ {0}</b></size>", ( j.Target != null ? Interpreter.Utils.CleanString(j.Target.Title) : "..." ));

                    var size = _contentStyle.CalcSize(new GUIContent(content));
                    size.x = Mathf.Max(size.x, 100);

                    var rect = new Rect(node.Pos.x, node.Pos.y, size.x, size.y);
                    _nodeRects[node.Id] = rect;

                    delayDraw += () =>
                    {
                        GUI.color = NODE_BASE_COLOR;
                        GUI.DrawTexture(rect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        GUI.Label(rect, content, _contentStyle);
                        Handles.color = NODE_DEFAULT_COLOR;
                        Handles.DrawAAConvexPolygon(new Vector2(rect.x, rect.y), new Vector2(rect.x + 5, rect.y), new Vector2(rect.x + 10, rect.center.y), new Vector2(rect.x + 5, rect.yMax), new Vector2(rect.x, rect.yMax));
                        Handles.color = Color.white;
                    };
                }
            }

            ///----------------------------------------------------------------------------------------------

            foreach ( var note in project.Boards[_currentBoardIndex].Notes ) {
                var content = Interpreter.Utils.CleanString(note.RawContent);
                var rect = new Rect(note.Pos.x, note.Pos.y, 220, _contentStyle.CalcHeight(new GUIContent(content), 220));
                rect.yMax = Mathf.CeilToInt(rect.yMax / GRID_SIZE) * GRID_SIZE;
                delayDraw += () =>
                {
                    GUI.color = COLOR_THEMES[note.ColorTheme];
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(rect, content, _contentStyle);
                };
            }

            ///----------------------------------------------------------------------------------------------

            foreach ( var node in project.Boards[_currentBoardIndex].Nodes ) {

                if ( node is Element ) {

                    var rect = _nodeRects[node.Id];
                    var e = node as Element;
                    foreach ( var connection in e.Outputs ) {
                        DrawConnection(connection, rect, _nodeRects[connection.Target.Id]);
                    }
                }

                if ( node is Branch ) {

                    var rect = _nodeRects[node.Id];
                    var b = node as Branch;
                    for ( var i = 0; i < b.Conditions.Count; i++ ) {
                        var connection = b.Conditions[i].Output;
                        var x = rect.xMax;
                        var y = rect.y + 15 + ( ( rect.height / b.Conditions.Count ) * i );
                        DrawCircle(new Vector2(x, y), connection.isValid);
                        if ( connection.isValid ) {
                            DrawConnection(connection, new Rect(x, y, 0, 0), _nodeRects[connection.Target.Id], Vector2.right);
                        }
                    }
                }
            }

            if ( delayDraw != null ) { delayDraw(); }
        }

        ///----------------------------------------------------------------------------------------------

        //...
        void DrawConnection(Connection connection, Rect source, Rect target, Vector2 forceSourceDir = default, Vector2 forceTargetDir = default) {
            var dir = Vector2.zero;
            var s = Vector2.zero;
            var t = Vector2.zero;
            var ts = Vector2.zero;
            var tt = Vector2.zero;
            var tangentStrength = 80;

            var hDistance = Mathf.Abs(source.center.x - target.center.x);
            var vDistance = Mathf.Abs(source.center.y - target.center.y);

            if ( hDistance >= vDistance ) {
                if ( target.x >= source.x ) {
                    dir = Vector2.right;
                    s.x = source.xMax;
                    s.y = source.center.y;
                    t.x = target.xMin;
                    t.y = target.y + 20;
                    ts = s + dir * tangentStrength;
                    tt = t - dir * tangentStrength;
                }

                if ( target.x < source.x ) {
                    dir = Vector2.left;
                    s.x = source.xMin;
                    s.y = source.center.y;
                    t.x = target.xMax;
                    t.y = target.yMax - 20;
                    ts = s + dir * tangentStrength;
                    tt = t - dir * tangentStrength;
                }
            }

            if ( vDistance > hDistance ) {
                if ( target.y > source.y ) {
                    dir = Vector2.down;
                    s.x = source.center.x;
                    s.y = source.yMax;
                    t.x = target.center.x > source.center.x ? target.x + 20 : target.xMax - 20;
                    t.y = target.yMin;
                    ts = s - dir * tangentStrength;
                    tt = t + dir * tangentStrength;
                }

                if ( target.y < source.y ) {
                    dir = Vector2.up;
                    s.x = source.center.x;
                    s.y = source.yMin;
                    t.x = target.center.x > source.center.x ? target.x + 20 : target.xMax - 20;
                    t.y = target.yMax;
                    ts = s - dir * tangentStrength;
                    tt = t + dir * tangentStrength;
                }
            }

            if ( forceSourceDir != default ) { ts = s + forceSourceDir * tangentStrength; }
            if ( forceTargetDir != default ) { tt = t + forceTargetDir * tangentStrength; }

            Handles.DrawBezier(s, t, ts, tt, Color.white, null, 3);
            DrawCircle(s);
            DrawArrow(t, dir);

            var label = Interpreter.Utils.CleanString(connection.RawLabel);
            if ( !string.IsNullOrEmpty(label) ) {
                var size = _labelStyle.CalcSize(new GUIContent(label));
                var labelRect = new Rect(0, 0, size.x, size.y);
                var midPos = GetPosAlongCurve(s, t, ts, tt, 0.5f);
                labelRect.center = midPos;
                GUI.color = NODE_DEFAULT_COLOR;
                GUI.DrawTexture(labelRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(labelRect, label, _labelStyle);
            }
        }

        //...
        void DrawArrow(Vector2 pos, Vector2 direction) {
            if ( direction == Vector2.right ) { Handles.DrawAAConvexPolygon(pos, pos + new Vector2(-7, -7), pos + new Vector2(-7, 7)); }
            if ( direction == Vector2.left ) { Handles.DrawAAConvexPolygon(pos, pos + new Vector2(7, 7), pos + new Vector2(7, -7)); }
            if ( direction == Vector2.up ) { Handles.DrawAAConvexPolygon(pos, pos + new Vector2(-7, 7), pos + new Vector2(7, 7)); }
            if ( direction == Vector2.down ) { Handles.DrawAAConvexPolygon(pos, pos + new Vector2(-7, -7), pos + new Vector2(7, -7)); }
        }

        //...
        void DrawCircle(Vector2 pos, bool solid = true) {
            if ( solid ) {
                Handles.DrawSolidDisc(pos, Vector3.forward, 5);
            } else {
                Handles.DrawWireDisc(pos, Vector3.forward, 5, 1);
            }
        }

        //...
        void DrawGrid(Rect container, Vector2 offset, float zoomFactor) {
            if ( Event.current.type != EventType.Repaint ) { return; }

            var hColor = Color.white;
            hColor.a = 0.04f;
            Handles.color = hColor;

            var drawGridSize = zoomFactor > 0.5f ? GRID_SIZE : GRID_SIZE * 5;
            var step = drawGridSize * zoomFactor;

            var xDiff = offset.x % step;
            var xStart = container.xMin + xDiff;
            var xEnd = container.xMax;
            for ( var i = xStart; i < xEnd; i += step ) {
                if ( i > container.xMin ) { Handles.DrawLine(new Vector3(i, container.yMin, 0), new Vector3(i, container.yMax, 0)); }
            }

            var yDiff = offset.y % step;
            var yStart = container.yMin + yDiff;
            var yEnd = container.yMax;
            for ( var i = yStart; i < yEnd; i += step ) {
                if ( i > container.yMin ) { Handles.DrawLine(new Vector3(container.xMin, i, 0), new Vector3(container.xMax, i, 0)); }
            }

            Handles.color = Color.white;
        }

        //...
        Rect StartZoomArea(Rect container, float zoomFactor, out Matrix4x4 oldMatrix) {
            GUI.EndClip();
            container.y += TOP_MARGIN;
            container.width *= 1 / zoomFactor;
            container.height *= 1 / zoomFactor;
            oldMatrix = GUI.matrix;
            var matrix1 = Matrix4x4.TRS(new Vector2(container.x, container.y), Quaternion.identity, Vector3.one);
            var matrix2 = Matrix4x4.Scale(new Vector3(zoomFactor, zoomFactor, 1f));
            GUI.matrix = matrix1 * matrix2 * matrix1.inverse * GUI.matrix;
            return container;
        }

        //...
        void EndZoomArea(Matrix4x4 oldMatrix) {
            GUI.matrix = oldMatrix;
            GUI.BeginClip(new Rect(0, TOP_MARGIN, position.width, position.height));
        }

        //...
        void PanTo(Vector2 targetPos) {
            pan = -targetPos;
            pan *= zoomFactor;
        }

        //...
        void ZoomAt(Vector2 center, float delta) {
            var pinPoint = ( center - pan ) / zoomFactor;
            var newZ = zoomFactor;
            newZ += delta;
            newZ = Mathf.Clamp(newZ, ZOOM_MIN, ZOOM_MAX);
            zoomFactor = newZ;

            var a = ( pinPoint * newZ ) + pan;
            var b = center;
            var diff = b - a;
            pan += diff;
        }

        //...
        Vector2 GetPosAlongCurve(Vector2 from, Vector2 to, Vector2 fromTangent, Vector2 toTangent, float t) {
            float u = 1.0f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            Vector2 result = uuu * from;
            result += 3 * uu * t * fromTangent;
            result += 3 * u * tt * toTangent;
            result += ttt * to;
            return result;
        }

    }
}

#endif