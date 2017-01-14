using System.Reflection;
using UnityEngine;

namespace UnityEditor.Extensions {
    public static class CustomConsoleFont {
        #region NESTED CLASSES
        private static class PrefKeys {
            public const string         Enabled             = "ConsoleFont.Enabled";
            public const string         FontGUID            = "ConsoleFont.GUID";
            public const string         FontSize            = "ConsoleFont.Size";
            public const string         ValidationFrequency = "ConsoleFont.ValidationFrequency";
        }
        #endregion

        #region FIELDS
        private static Font         _ConsoleFont;
        private static int          _LastConsoleInstanceID;
        private static FieldInfo    _ConsoleInstanceField;
        private static MethodInfo   _ConsoleGetInstanceIDMethod;
        private static double       _NextInstanceIDCheck        = 0;
        private static bool         _IsDirty;
        private static bool         _IsInitialized          = false;
        #endregion

        #region INITIALIZATION
        [InitializeOnLoadMethod]
        private static void OnProjectLoaded() {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            _IsDirty = true;
            _IsInitialized = true;
        }
        #endregion

        #region UPDATE METHODS
        private static void OnEditorUpdate() {
            if (_NextInstanceIDCheck <= EditorApplication.timeSinceStartup) {
                var iid = ConsoleInstanceID;

                if (iid != _LastConsoleInstanceID)
                    _IsDirty = true;

                _LastConsoleInstanceID = iid;
                _NextInstanceIDCheck = EditorApplication.timeSinceStartup + ValidationFrequency;
            }
            if (_IsDirty) {
                EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
                _IsDirty = false;
                EditorApplication.RepaintProjectWindow();
            }
        }

        private static void OnProjectWindowItemGUI(string guid, UnityEngine.Rect selectionRect) {
            if (Enabled) {
                SetConsoleFont(ConsoleFont, ConsoleFontSize);
            } else {
                ResetConsoleFont();
            }
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
            RepaintConsole();
        }
        #endregion

        #region GUI (preferences)
        [PreferenceItem("Console Font")]
        private static void PreferencesGUI() {
            if (!_IsInitialized)
                OnProjectLoaded();

            EditorGUIUtility.labelWidth = 140;
            Enabled = EditorGUILayout.ToggleLeft("Enabled", Enabled);

            GUI.enabled = Enabled;

            var newFont = EditorGUILayout.ObjectField("Font", ConsoleFont, typeof(Font), false);
            if (newFont != ConsoleFont)
                ConsoleFontGUID = newFont == null ? string.Empty : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(newFont));

            var newSize = EditorGUILayout.IntSlider("Font Size", ConsoleFontSize, 4, 16);
            if (newSize != ConsoleFontSize)
                ConsoleFontSize = newSize;

            ValidationFrequency = EditorGUILayout.Slider("Validation Frequency", ValidationFrequency, 0.05f, 1f);
        }
        #endregion

        #region PRIVATE METHODS (GENERAL)
        private static void RepaintConsole() {
            if (ConsoleInstanceExist)
                ConsoleType.GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public).Invoke(ConsoleInstanceField.GetValue(null), null);
        }

        private static void ResetConsoleFont() {
            if (!ConsoleInstanceExist)
                return;

            var tConstants = Assembly.GetAssembly(typeof(UnityEditor.EditorWindow)).GetType("UnityEditor.ConsoleWindow").GetNestedType("Constants", BindingFlags.NonPublic);
            tConstants.GetField("ms_Loaded", BindingFlags.Static | BindingFlags.Public).SetValue(null, false);
            tConstants.GetMethod("Init", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
        }

        private static void SetConsoleFont(Font font, int fontSize) {
            if (!ConsoleInstanceExist)
                return;

            var tConstants = ConsoleConstantsType;

            if (!(bool)(tConstants.GetField("ms_Loaded", BindingFlags.Static | BindingFlags.Public).GetValue(null))) {
                tConstants.GetMethod("Init", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
            }

            FieldInfo fiNormal = tConstants.GetField("LogStyle", BindingFlags.Static | BindingFlags.Public),
                        fiWarning = tConstants.GetField("WarningStyle", BindingFlags.Static | BindingFlags.Public),
                        fiError = tConstants.GetField("ErrorStyle", BindingFlags.Static | BindingFlags.Public),
                        fiMessage = tConstants.GetField("MessageStyle", BindingFlags.Static | BindingFlags.Public);

            GUIStyle gsNormal = new GUIStyle((GUIStyle)fiNormal.GetValue(null)),
                        gsWarning = new GUIStyle((GUIStyle)fiWarning.GetValue(null)),
                        gsError = new GUIStyle((GUIStyle)fiError.GetValue(null)),
                        gsMessage = new GUIStyle((GUIStyle)fiMessage.GetValue(null));

            gsNormal.font = gsWarning.font = gsError.font = gsMessage.font = font;
            gsNormal.fontSize = gsWarning.fontSize = gsError.fontSize = gsMessage.fontSize = fontSize;

            fiNormal.SetValue(null, gsNormal);
            fiWarning.SetValue(null, gsWarning);
            fiError.SetValue(null, gsError);
            fiMessage.SetValue(null, gsMessage);
        }
        #endregion

        #region PROPERTIES
        private static System.Type ConsoleType {
            get { return EditorAssembly.GetType("UnityEditor.ConsoleWindow"); }
        }

        private static System.Type ConsoleConstantsType {
            get { return ConsoleType.GetNestedType("Constants", BindingFlags.NonPublic); }
        }

        private static bool ConsoleInstanceExist {
            get { return ConsoleInstanceField.GetValue(null) != null; }
        }

        private static FieldInfo ConsoleInstanceField {
            get {
                if (_ConsoleInstanceField == null)
                    _ConsoleInstanceField = ConsoleType.GetField("ms_ConsoleWindow", BindingFlags.Static | BindingFlags.NonPublic);
                return _ConsoleInstanceField;
            }
        }

        private static int ConsoleInstanceID {
            get {
                if (_ConsoleGetInstanceIDMethod == null && ConsoleInstanceExist)
                    _ConsoleGetInstanceIDMethod = ConsoleType.GetMethod("GetInstanceID", BindingFlags.Instance | BindingFlags.Public);
                return _ConsoleGetInstanceIDMethod == null ? 0 : (int)_ConsoleGetInstanceIDMethod.Invoke(ConsoleInstanceField.GetValue(null), null);
            }
        }

        private static Font ConsoleFont {
            get {
                if (_ConsoleFont == null && !string.IsNullOrEmpty(ConsoleFontGUID)) {
                    var fontPath = AssetDatabase.GUIDToAssetPath(ConsoleFontGUID);
                    if (!string.IsNullOrEmpty(fontPath))
                        _ConsoleFont = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
                }
                return _ConsoleFont;
            }
        }

        private static string ConsoleFontGUID {
            get { return EditorPrefs.GetString(PrefKeys.FontGUID, string.Empty); }
            set {
                if (ConsoleFontGUID == value)
                    return;
                if (string.IsNullOrEmpty(value) || AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(value)) == null) {
                    EditorPrefs.DeleteKey(PrefKeys.FontGUID);
                } else {
                    EditorPrefs.SetString(PrefKeys.FontGUID, value);
                }
                _IsDirty = true;
                _ConsoleFont = null;
            }
        }

        private static int ConsoleFontSize {
            get { return EditorPrefs.GetInt(PrefKeys.FontSize, 11); }
            set {
                value = Mathf.Clamp(value, 4, 16);
                if (ConsoleFontSize == value)
                    return;

                EditorPrefs.SetInt(PrefKeys.FontSize, value);
                _IsDirty = true;
            }
        }

        private static Assembly EditorAssembly {
            get { return Assembly.GetAssembly(typeof(UnityEditor.EditorWindow)); }
        }

        private static bool Enabled {
            get { return EditorPrefs.GetBool(PrefKeys.Enabled, true); }
            set {
                if (Enabled == value)
                    return;
                EditorPrefs.SetBool(PrefKeys.Enabled, value);
                _IsDirty = true;
            }
        }

        private static float ValidationFrequency {
            get { return EditorPrefs.GetFloat(PrefKeys.ValidationFrequency, 0.5f); }
            set {
                if (value == ValidationFrequency)
                    return;
                _NextInstanceIDCheck += (value - ValidationFrequency);
                EditorPrefs.SetFloat(PrefKeys.ValidationFrequency, value);
            }
        }
        #endregion
    }
}