using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mekabu.BlendshapeAnimationCreator.Editor
{
    internal sealed class BlendshapeAnimationCreatorWindow : EditorWindow
    {
        private const float ControlsWidth = 460f;
        private const float PreviewMinWidth = 320f;

        private readonly List<BlendshapeEntry> _blendshapes = new List<BlendshapeEntry>();

        private GameObject _avatarRoot;
        private SkinnedMeshRenderer _targetRenderer;
        private Vector2 _blendshapeScroll;
        private string _search = string.Empty;
        private string _clipName = "New Expression";
        private AnimationClip _editingClip;

        private PreviewRenderUtility _previewUtility;
        private GameObject _previewRoot;
        private SkinnedMeshRenderer _previewRenderer;
        private float _previewYaw = 180f;
        private float _previewPitch;
        private float _previewZoom = 1f;
        private Vector2 _previewPan;
        private PreviewFraming _previewFraming = PreviewFraming.Face;

        [MenuItem("Mekabu/Blendshape Animator")]
        private static void Open()
        {
            var window = GetWindow<BlendshapeAnimationCreatorWindow>();
            window.titleContent = new GUIContent("Blendshape Animator");
            window.minSize = new Vector2(320f, 480f);
            window.Show();
        }

        private void OnDisable()
        {
            DisposePreview();
        }

        private void OnGUI()
        {
            if (UseVerticalLayout())
            {
                EditorGUILayout.BeginVertical();
                DrawControls(true);
                DrawPreviewPanel(true);
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                DrawControls(false);
                DrawPreviewPanel(false);
                EditorGUILayout.EndHorizontal();
            }
        }

        private bool UseVerticalLayout()
        {
            return position.width < 720f || position.height > position.width * 1.1f;
        }

        private void DrawControls(bool verticalLayout)
        {
            if (verticalLayout)
            {
                EditorGUILayout.BeginVertical(
                    GUILayout.Height(Mathf.Max(230f, position.height * 0.5f)),
                    GUILayout.ExpandWidth(true));
            }
            else
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(ControlsWidth));
            }
            EditorGUILayout.LabelField("Blendshape Animator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Values edited here are kept only by this window. The source avatar and prefab are not modified.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            var avatarRoot = (GameObject)EditorGUILayout.ObjectField(
                "Avatar Root",
                _avatarRoot,
                typeof(GameObject),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                SetAvatarRoot(avatarRoot);
            }

            using (new EditorGUI.DisabledScope(_avatarRoot == null))
            {
                EditorGUI.BeginChangeCheck();
                var targetRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
                    "Face Renderer",
                    _targetRenderer,
                    typeof(SkinnedMeshRenderer),
                    true);
                if (EditorGUI.EndChangeCheck())
                {
                    SetTargetRenderer(targetRenderer);
                }
            }

            if (_avatarRoot != null && _targetRenderer == null)
            {
                EditorGUILayout.HelpBox(
                    "No SkinnedMeshRenderer named 'Body' was found. Drag the face mesh into Face Renderer.",
                    MessageType.Warning);
            }

            if (_targetRenderer != null && _blendshapes.Count == 0)
            {
                EditorGUILayout.HelpBox("The selected renderer has no blendshapes.", MessageType.Warning);
            }

            DrawClipControls();
            DrawBlendshapeToolbar();
            DrawBlendshapeList();
            DrawApplyControls();
            EditorGUILayout.EndVertical();
        }

        private void DrawClipControls()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Animation Clip", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var editingClip = (AnimationClip)EditorGUILayout.ObjectField(
                "Editing Clip",
                _editingClip,
                typeof(AnimationClip),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                SetEditingClip(editingClip);
            }

            EditorGUILayout.BeginHorizontal();
            _clipName = EditorGUILayout.TextField("New Clip Name", _clipName);
            if (GUILayout.Button("Create", GUILayout.Width(64f)))
            {
                CreateEmptyAnimationClip();
            }
            EditorGUILayout.EndHorizontal();

            if (_editingClip != null)
            {
                EditorGUILayout.LabelField(
                    AssetDatabase.GetAssetPath(_editingClip),
                    EditorStyles.miniLabel);
            }
        }

        private void DrawBlendshapeToolbar()
        {
            using (new EditorGUI.DisabledScope(_blendshapes.Count == 0))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("Initial", EditorStyles.miniButton, GUILayout.Width(52f)))
                {
                    ResetToInitialValues();
                }
                if (GUILayout.Button("Zero", EditorStyles.miniButton, GUILayout.Width(42f)))
                {
                    SetAllValuesToZero();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Include in clip:", GUILayout.Width(90f));
                if (GUILayout.Button("Changed", EditorStyles.miniButtonLeft))
                {
                    IncludeChangedValues();
                }
                if (GUILayout.Button("All", EditorStyles.miniButtonMid))
                {
                    SetAllIncluded(true);
                }
                if (GUILayout.Button("None", EditorStyles.miniButtonRight))
                {
                    SetAllIncluded(false);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawBlendshapeList()
        {
            _blendshapeScroll = EditorGUILayout.BeginScrollView(_blendshapeScroll);
            for (var index = 0; index < _blendshapes.Count; index++)
            {
                var entry = _blendshapes[index];
                if (!MatchesSearch(entry.Name))
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                entry.Include = EditorGUILayout.Toggle(entry.Include, GUILayout.Width(18f));
                EditorGUILayout.LabelField(entry.Name, GUILayout.Width(190f));

                EditorGUI.BeginChangeCheck();
                var value = EditorGUILayout.Slider(entry.Value, 0f, 100f);
                if (EditorGUI.EndChangeCheck())
                {
                    entry.Value = value;
                    entry.Include = true;
                    ApplyPreviewValues();
                    Repaint();
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawApplyControls()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(
                       _editingClip == null || _targetRenderer == null || _blendshapes.Count == 0))
            {
                if (GUILayout.Button("Apply Blendshapes to Clip", GUILayout.Height(28f)))
                {
                    ApplyBlendshapesToClip();
                }
            }
        }

        private void DrawPreviewPanel(bool verticalLayout)
        {
            if (verticalLayout)
            {
                EditorGUILayout.BeginVertical(
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));
            }
            else
            {
                EditorGUILayout.BeginVertical(
                    GUILayout.MinWidth(PreviewMinWidth),
                    GUILayout.ExpandWidth(true));
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Preview", GUILayout.Width(48f));
            _previewFraming = (PreviewFraming)GUILayout.Toolbar(
                (int)_previewFraming,
                new[] { "Face", "Full" },
                EditorStyles.toolbarButton,
                GUILayout.Width(100f));
            if (!verticalLayout)
            {
                GUILayout.Label("L-drag: rotate  R-drag: pan  Wheel: zoom", EditorStyles.miniLabel);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset View", EditorStyles.toolbarButton, GUILayout.Width(76f)))
            {
                ResetPreviewView();
            }
            EditorGUILayout.EndHorizontal();

            var previewRect = GUILayoutUtility.GetRect(
                verticalLayout ? 0f : PreviewMinWidth,
                2000f,
                320f,
                2000f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));

            DrawPreview(previewRect);
            HandlePreviewInput(previewRect);
            EditorGUILayout.EndVertical();
        }

        private void SetAvatarRoot(GameObject avatarRoot)
        {
            _avatarRoot = avatarRoot;
            _targetRenderer = FindDefaultBodyRenderer(avatarRoot);
            LoadBlendshapesFromTarget();
        }

        private void SetTargetRenderer(SkinnedMeshRenderer targetRenderer)
        {
            if (targetRenderer != null &&
                (_avatarRoot == null || !targetRenderer.transform.IsChildOf(_avatarRoot.transform)))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Face Renderer",
                    "Face Renderer must be a child of the selected Avatar Root.",
                    "OK");
                return;
            }

            _targetRenderer = targetRenderer;
            LoadBlendshapesFromTarget();
        }

        private static SkinnedMeshRenderer FindDefaultBodyRenderer(GameObject avatarRoot)
        {
            if (avatarRoot == null)
            {
                return null;
            }

            var renderers = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                if (string.Equals(renderer.name, "Body", StringComparison.OrdinalIgnoreCase))
                {
                    return renderer;
                }
            }

            return renderers.Length > 0 ? renderers[0] : null;
        }

        private void LoadBlendshapesFromTarget()
        {
            _blendshapes.Clear();

            var mesh = _targetRenderer != null ? _targetRenderer.sharedMesh : null;
            if (mesh != null)
            {
                for (var index = 0; index < mesh.blendShapeCount; index++)
                {
                    var initialValue = _targetRenderer.GetBlendShapeWeight(index);
                    _blendshapes.Add(new BlendshapeEntry(
                        index,
                        mesh.GetBlendShapeName(index),
                        initialValue));
                }
            }

            ResetPreviewView();
            RebuildPreview();
            LoadValuesFromEditingClip();
        }

        private void ResetToInitialValues()
        {
            foreach (var entry in _blendshapes)
            {
                entry.Value = entry.InitialValue;
                entry.Include = false;
            }

            ApplyPreviewValues();
            Repaint();
        }

        private void SetAllValuesToZero()
        {
            foreach (var entry in _blendshapes)
            {
                entry.Value = 0f;
                entry.Include = !Mathf.Approximately(entry.InitialValue, 0f);
            }

            ApplyPreviewValues();
            Repaint();
        }

        private void IncludeChangedValues()
        {
            foreach (var entry in _blendshapes)
            {
                entry.Include = !Mathf.Approximately(entry.Value, entry.InitialValue);
            }
        }

        private void SetAllIncluded(bool included)
        {
            foreach (var entry in _blendshapes)
            {
                entry.Include = included;
            }
        }

        private bool MatchesSearch(string blendshapeName)
        {
            return string.IsNullOrWhiteSpace(_search) ||
                   blendshapeName.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SetEditingClip(AnimationClip clip)
        {
            _editingClip = clip;
            if (_editingClip != null)
            {
                _clipName = _editingClip.name;
            }

            LoadValuesFromEditingClip();
        }

        private void LoadValuesFromEditingClip()
        {
            foreach (var entry in _blendshapes)
            {
                entry.Value = entry.InitialValue;
                entry.Include = false;
            }

            if (_editingClip == null || _avatarRoot == null || _targetRenderer == null)
            {
                ApplyPreviewValues();
                Repaint();
                return;
            }

            var relativePath = AnimationUtility.CalculateTransformPath(
                _targetRenderer.transform,
                _avatarRoot.transform);
            var entriesByName = new Dictionary<string, BlendshapeEntry>(StringComparer.Ordinal);
            foreach (var entry in _blendshapes)
            {
                entriesByName[entry.Name] = entry;
            }

            foreach (var binding in AnimationUtility.GetCurveBindings(_editingClip))
            {
                if (binding.type != typeof(SkinnedMeshRenderer) ||
                    !string.Equals(binding.path, relativePath, StringComparison.Ordinal) ||
                    !binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
                {
                    continue;
                }

                var blendshapeName = binding.propertyName.Substring("blendShape.".Length);
                if (!entriesByName.TryGetValue(blendshapeName, out var entry))
                {
                    continue;
                }

                var curve = AnimationUtility.GetEditorCurve(_editingClip, binding);
                if (curve == null || curve.length == 0)
                {
                    continue;
                }

                entry.Value = curve.Evaluate(0f);
                entry.Include = true;
            }

            ApplyPreviewValues();
            Repaint();
        }

        private void CreateEmptyAnimationClip()
        {
            var defaultFolder = FindDefaultAnimationFolder();
            var safeClipName = string.IsNullOrWhiteSpace(_clipName) ? "New Expression" : _clipName.Trim();
            var assetPath = EditorUtility.SaveFilePanelInProject(
                "Create Animation Clip",
                safeClipName,
                "anim",
                "Choose where to create the facial expression animation.",
                defaultFolder);

            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
            {
                EditorUtility.DisplayDialog(
                    "Asset Already Exists",
                    "Choose a new file name. Existing animation clips are not overwritten.",
                    "OK");
                return;
            }

            var clip = new AnimationClip
            {
                name = Path.GetFileNameWithoutExtension(assetPath),
                frameRate = 60f
            };

            AssetDatabase.CreateAsset(clip, assetPath);
            AssetDatabase.SaveAssets();
            SetEditingClip(clip);
            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);
        }

        private void ApplyBlendshapesToClip()
        {
            var clipPath = AssetDatabase.GetAssetPath(_editingClip);
            if (!AssetDatabase.IsMainAsset(_editingClip) ||
                !string.Equals(Path.GetExtension(clipPath), ".anim", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(
                    "Animation Clip Is Not Editable",
                    "Use a standalone .anim asset. Imported model clips and package sub-assets cannot be overwritten.",
                    "OK");
                return;
            }

            var relativePath = AnimationUtility.CalculateTransformPath(
                _targetRenderer.transform,
                _avatarRoot.transform);

            Undo.RecordObject(_editingClip, "Apply Blendshape Animation");

            // Remove only the blendshape curves for the selected face renderer.
            // Curves for other components or renderers remain untouched.
            foreach (var binding in AnimationUtility.GetCurveBindings(_editingClip))
            {
                if (binding.type == typeof(SkinnedMeshRenderer) &&
                    string.Equals(binding.path, relativePath, StringComparison.Ordinal) &&
                    binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
                {
                    AnimationUtility.SetEditorCurve(_editingClip, binding, null);
                }
            }

            foreach (var entry in _blendshapes)
            {
                if (!entry.Include)
                {
                    continue;
                }

                var binding = EditorCurveBinding.FloatCurve(
                    relativePath,
                    typeof(SkinnedMeshRenderer),
                    "blendShape." + entry.Name);
                var curve = new AnimationCurve(new Keyframe(0f, entry.Value));
                AnimationUtility.SetEditorCurve(_editingClip, binding, curve);
            }

            EditorUtility.SetDirty(_editingClip);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(_editingClip);
            ShowNotification(new GUIContent("Blendshapes applied to " + _editingClip.name));
        }

        private string FindDefaultAnimationFolder()
        {
            var sourcePath = AssetDatabase.GetAssetPath(_avatarRoot);
            if (string.IsNullOrEmpty(sourcePath) && _avatarRoot.scene.IsValid())
            {
                sourcePath = _avatarRoot.scene.path;
            }

            sourcePath = sourcePath.Replace('\\', '/');
            foreach (var marker in new[] { "/Scenes/", "/Prefabs/" })
            {
                var markerIndex = sourcePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex >= 0)
                {
                    var folder = sourcePath.Substring(0, markerIndex) + "/Animations";
                    if (AssetDatabase.IsValidFolder(folder))
                    {
                        return folder;
                    }
                }
            }

            return "Assets";
        }

        private void RebuildPreview()
        {
            DisposePreview();
            if (_avatarRoot == null || _targetRenderer == null)
            {
                return;
            }

            _previewUtility = new PreviewRenderUtility();
            _previewUtility.cameraFieldOfView = 30f;
            _previewUtility.ambientColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            _previewUtility.lights[0].intensity = 1.1f;
            _previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            _previewUtility.lights[1].intensity = 0.6f;

            _previewRoot = Instantiate(_avatarRoot);
            _previewRoot.name = _avatarRoot.name;
            _previewRoot.hideFlags = HideFlags.HideAndDontSave;
            _previewRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var targetPath = AnimationUtility.CalculateTransformPath(
                _targetRenderer.transform,
                _avatarRoot.transform);
            var previewTarget = string.IsNullOrEmpty(targetPath)
                ? _previewRoot.transform
                : _previewRoot.transform.Find(targetPath);
            _previewRenderer = previewTarget != null
                ? previewTarget.GetComponent<SkinnedMeshRenderer>()
                : null;

            _previewUtility.AddSingleGO(_previewRoot);
            ApplyPreviewValues();
        }

        private void DisposePreview()
        {
            if (_previewUtility != null)
            {
                _previewUtility.Cleanup();
                _previewUtility = null;
            }

            _previewRoot = null;
            _previewRenderer = null;
        }

        private void ApplyPreviewValues()
        {
            if (_previewRenderer == null || _previewRenderer.sharedMesh == null)
            {
                return;
            }

            foreach (var entry in _blendshapes)
            {
                if (entry.Index < _previewRenderer.sharedMesh.blendShapeCount)
                {
                    _previewRenderer.SetBlendShapeWeight(entry.Index, entry.Value);
                }
            }
        }

        private void DrawPreview(Rect previewRect)
        {
            if (_previewUtility == null || _previewRoot == null)
            {
                EditorGUI.DrawRect(previewRect, new Color(0.16f, 0.16f, 0.16f));
                GUI.Label(previewRect, "Select an Avatar Root to preview expressions.", CenteredLabelStyle());
                return;
            }

            var avatarBounds = CalculatePreviewBounds();
            var bounds = _previewFraming == PreviewFraming.Face && _previewRenderer != null
                ? _previewRenderer.bounds
                : avatarBounds;
            var focus = bounds.center;
            var framingSize = Mathf.Max(bounds.extents.magnitude, 0.1f);

            if (_previewFraming == PreviewFraming.Face)
            {
                var animator = _previewRoot.GetComponentInChildren<Animator>(true);
                var head = animator != null && animator.isHuman
                    ? animator.GetBoneTransform(HumanBodyBones.Head)
                    : null;

                if (head != null)
                {
                    // The Humanoid head bone remains a stable face anchor even when
                    // hair, clothes, or accessories make the bounds asymmetric.
                    focus = head.position + Vector3.up * bounds.extents.y * 0.06f;
                }
                else
                {
                    // Non-Humanoid fallback: use the upper part of the selected mesh.
                    focus = bounds.center + Vector3.up * bounds.extents.y * 0.7f;
                }

                framingSize = Mathf.Max(bounds.extents.y * 0.2f, 0.12f);
            }

            var cameraRotation = Quaternion.Euler(_previewPitch, _previewYaw, 0f);
            focus += cameraRotation * new Vector3(
                _previewPan.x * framingSize,
                _previewPan.y * framingSize,
                0f);
            var halfFovRadians = _previewUtility.cameraFieldOfView * 0.5f * Mathf.Deg2Rad;
            var distance = framingSize / Mathf.Tan(halfFovRadians) * _previewZoom;

            _previewUtility.camera.transform.position = focus - cameraRotation * Vector3.forward * distance;
            _previewUtility.camera.transform.rotation = cameraRotation;
            _previewUtility.camera.nearClipPlane = Mathf.Max(distance * 0.01f, 0.01f);
            _previewUtility.camera.farClipPlane = Mathf.Max(distance * 10f, 100f);

            _previewUtility.BeginPreview(previewRect, GUIStyle.none);
            _previewUtility.camera.Render();
            var previewTexture = _previewUtility.EndPreview();
            GUI.DrawTexture(previewRect, previewTexture, ScaleMode.StretchToFill, false);
        }

        private Bounds CalculatePreviewBounds()
        {
            var renderers = _previewRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one * 2f);
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private void HandlePreviewInput(Rect previewRect)
        {
            var currentEvent = Event.current;
            if (!previewRect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0)
            {
                _previewYaw += currentEvent.delta.x * 0.5f;
                _previewPitch = Mathf.Clamp(_previewPitch - currentEvent.delta.y * 0.5f, -80f, 80f);
                currentEvent.Use();
                Repaint();
            }
            else if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 1)
            {
                // Move the viewed model with the pointer. The offset is normalized
                // against the current framing size in DrawPreview.
                _previewPan += new Vector2(-currentEvent.delta.x, currentEvent.delta.y) * 0.0025f;
                currentEvent.Use();
                Repaint();
            }
            else if (currentEvent.type == EventType.ScrollWheel)
            {
                _previewZoom = Mathf.Clamp(_previewZoom * (1f + currentEvent.delta.y * 0.05f), 0.2f, 5f);
                currentEvent.Use();
                Repaint();
            }
        }

        private void ResetPreviewView()
        {
            _previewYaw = 180f;
            _previewPitch = 0f;
            _previewZoom = 1f;
            _previewPan = Vector2.zero;
            Repaint();
        }

        private static GUIStyle CenteredLabelStyle()
        {
            return new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }

        private sealed class BlendshapeEntry
        {
            public BlendshapeEntry(int index, string name, float initialValue)
            {
                Index = index;
                Name = name;
                InitialValue = initialValue;
                Value = initialValue;
            }

            public int Index { get; }
            public string Name { get; }
            public float InitialValue { get; }
            public float Value { get; set; }
            public bool Include { get; set; }
        }

        private enum PreviewFraming
        {
            Face,
            Full
        }
    }
}
