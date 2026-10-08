using UnityEngine;
using UnityEditor;
using UnityEngine.VFX;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace GenericDataStreaming.EditorScripts
{
    public static class DataStreamEditorUtils
    {
        /// <summary>
        /// Restituisce un array con i nomi formattati di tutti i componenti di un GameObject.
        /// </summary>
        public static string[] GetAvailableComponents(GameObject go, out Component[] components)
        {
            if (go == null)
            {
                components = new Component[0];
                return new string[0];
            }
            components = go.GetComponents<Component>();
            return components.Select(c => c.GetType().Name).ToArray();
        }

        /// <summary>
        /// Routing intelligente: capisce che componente è e restituisce la lista di proprietà adatte (float o Color).
        /// </summary>
        public static string[] GetPropertiesForComponent(Component comp, bool isColor = false)
        {
            if (comp == null) return new string[0];

            if (comp is VisualEffect vfx)
                return GetVfxProperties(vfx, isColor);
            
            if (comp is Renderer r)
                return GetMaterialProperties(r, isColor);

            if (comp is Transform t && !isColor)
                return new string[] { "UniformScale", "ScaleX", "ScaleY", "ScaleZ" };

            return GetComponentProperties(comp, isColor);
        }

        public static string DrawPropertyDropdown(string label, string currentValue, string[] options)
        {
            if (options == null || options.Length == 0)
            {
                EditorGUILayout.HelpBox("Nessuna proprietà valida trovata per questo componente.", MessageType.Warning);
                return currentValue;
            }

            int currentIndex = Mathf.Max(0, System.Array.IndexOf(options, currentValue));
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            return options[newIndex];
        }

        private static string[] GetVfxProperties(VisualEffect vfx, bool isColor)
        {
            if (vfx == null || vfx.visualEffectAsset == null) return new string[0];
            var list = new List<VFXExposedProperty>();
            vfx.visualEffectAsset.GetExposedProperties(list);
            
            // Il VFX Graph usa spessissimo Vector4 per i colori
            if (isColor) return list.Where(p => p.type == typeof(Vector4) || p.type == typeof(Color)).Select(p => p.name).ToArray();
            return list.Where(p => p.type == typeof(float)).Select(p => p.name).ToArray();
        }

        private static string[] GetMaterialProperties(Renderer renderer, bool isColor)
        {
            if (renderer == null || renderer.sharedMaterial == null) return new string[0];
            Shader shader = renderer.sharedMaterial.shader;
            List<string> props = new List<string>();
            for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
            {
                var type = ShaderUtil.GetPropertyType(shader, i);
                if (isColor && type == ShaderUtil.ShaderPropertyType.Color) props.Add(ShaderUtil.GetPropertyName(shader, i));
                else if (!isColor && (type == ShaderUtil.ShaderPropertyType.Float || type == ShaderUtil.ShaderPropertyType.Range)) props.Add(ShaderUtil.GetPropertyName(shader, i));
            }
            return props.ToArray();
        }

        private static string[] GetComponentProperties(Component comp, bool isColor)
        {
            if (comp == null) return new string[0];
            var type = isColor ? typeof(Color) : typeof(float);
            
            var fields = comp.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.FieldType == type || (!isColor && f.FieldType.IsEnum))
                .Select(f => f.Name);
                
            var properties = comp.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => (p.PropertyType == type || (!isColor && p.PropertyType.IsEnum)) && p.CanWrite)
                .Select(p => p.Name);
                
            return fields.Concat(properties).ToArray();
        }
    }
}
