using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace gishadev.tools.Core
{
#if UNITY_EDITOR
    public static class CodeGenerator
    {
        public static void GenerateExtensionsClass()
        {
            string path = "Assets/Generated/" + "GeneratedExtensionMethods" + ".cs";

            var str = new StringBuilder();
            str.AppendFormat("using gishadev.tools.Audio;");
            str.AppendLine();
            str.AppendFormat("using gishadev.tools.Effects;");
            str.AppendLine();
            str.AppendFormat("using UnityEngine;");
            str.AppendLine();
            str.AppendFormat("public static class GeneratedExtensionMethods");
            str.AppendLine();
            str.AppendLine("{");
            str.AppendFormat("public static void PlayMusic(this IAudioManager @this, MusicAudioEnum music) => @this.PlayMusic((int)music);");
            str.AppendLine();
            str.AppendFormat("public static void PlaySFX(this IAudioManager @this, SFXAudioEnum sfx) => @this.PlaySFX((int)sfx);");
            str.AppendLine();
            str.AppendFormat("public static GameObject EmitAt(this ISFXEmitter @this, SoundEffectsEnum sfx, Vector3 position, Quaternion rotation) => @this.EmitAt((int)sfx, position, rotation);");
            str.AppendLine();
            str.AppendFormat("public static GameObject EmitAt(this IVFXEmitter @this, VisualEffectsEnum vfx, Vector3 position, Quaternion rotation) => @this.EmitAt((int)vfx, position, rotation);");
            str.AppendLine();
            str.AppendFormat("public static GameObject EmitAt(this IOtherEmitter @this, OtherPoolEnum other, Vector3 position, Quaternion rotation) => @this.EmitAt((int)other, position, rotation);");
            str.AppendLine();

            str.AppendLine("}");

            FileInfo file = new FileInfo(path);
            file.Directory?.Create();

            File.WriteAllText(path, str.ToString());
            AssetDatabase.ImportAsset(path);
        }
        
        public static void GenerateEnumClass(string enumName, string[] enumEntries)
        {
            string path = "Assets/Generated/" + enumName + ".cs";

            var str = new StringBuilder();
            str.AppendFormat("public enum {0}", enumName);
            str.AppendLine();
            str.AppendLine("{");

            for (var i = 0; i < enumEntries.Length; i++)
            {
                str.AppendFormat("\t{0}", ValidateEnumString(enumEntries[i]));

                if (i < enumEntries.Length - 1)
                {
                    str.Append(',');
                    str.AppendLine();
                }
            }

            str.AppendLine();
            str.AppendLine(@"}");

            FileInfo file = new FileInfo(path);
            file.Directory?.Create();

            File.WriteAllText(path, str.ToString());
            AssetDatabase.ImportAsset(path);
        }

        private static string ValidateEnumString(string text)
        {
            if (text.Length <= 0)
            {
                Debug.LogError("Invalid enum name");
                return null;
            }

            // Turn all whitespaces into _.
            string whitespacePattern = @"\s+";
            string replacement = "_";
            var result = Regex.Replace(text, whitespacePattern, replacement).ToUpper();

            // First char must be a letter or an underscore.
            string specialCharactersPattern = @"[^\w\d]+";
            result = Regex.Replace(result, specialCharactersPattern, "_");

            return result;
        }
    }
#endif
}