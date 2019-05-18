using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LDraw
{
    public class LDrawConfig : ScriptableObject
    {
        [SerializeField] private string _BasePartsPath;
       
        [SerializeField] private string _ModelsPath;
        [SerializeField] private string _ColorConfigPath;
        [SerializeField] private string _MaterialsPath;
        [SerializeField] private string _MeshesPath;
        [SerializeField] private float _Scale;
        [SerializeField] private Material _DefaultOpaqueMaterial;
        [SerializeField] private Material _DefaultTransparentMaterial;
        private Dictionary<string, string> _Parts;
        private Dictionary<string, string> _Models;
        
        private Dictionary<int, Material> _MainColors;
        private Dictionary<string, Material> _CustomColors;
        private Dictionary<string, string> _ModelFileNames;
        public Matrix4x4 ScaleMatrix
        {
            get { return Matrix4x4.Scale(new Vector3(_Scale, _Scale, _Scale)); }
        }

        public Material GetColoredMaterial(int code)
        {
            return _MainColors[code];
        }
        public Material GetColoredMaterial(string colorString)
        {
            if (_CustomColors.ContainsKey(colorString))
                return _CustomColors[colorString];
            var path = _MaterialsPath + colorString + ".mat";
            if (File.Exists(path))
            {
                _CustomColors.Add(colorString, AssetDatabase.LoadAssetAtPath<Material>(path));
            }
            else
            {
                var mat = new Material(_DefaultOpaqueMaterial);
                 
                mat.name = colorString;
                Color color;
                if (ColorUtility.TryParseHtmlString(colorString, out color))
                    mat.color = color;
                            
                AssetDatabase.CreateAsset(mat, path);
                AssetDatabase.SaveAssets();
                _CustomColors.Add(colorString, mat);
            }

            return _CustomColors[colorString];
        }
        public string[] ModelFileNames
        {
            get { return _ModelFileNames.Keys.ToArray(); }
        }

        public string GetModelByFileName(string modelFileName)
        {
            return _ModelFileNames[modelFileName];
        }
        public string GetSerializedPart(string name)
        {
            try
            {
                name = name.ToLower();
           
                var serialized = _Parts.ContainsKey(name) ? File.ReadAllText(_Parts[name]) : _Models[name]; 
                return serialized;
            }
            catch
            {
                Debug.Log("http://www.ldraw.org/library/tracker/");
                EditorUtility.DisplayDialog("Error!", "Missing part or wrong part " + name 
                                                        + "! Find it in url from debug console", "Ok", "");
                throw;
            }
        
        }

        public void InitParts()
        { 
            PrepareModels();
            ParseColors();
            _Parts = new Dictionary<string, string>();
            var files = Directory.GetFiles(_BasePartsPath, "*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                if (!file.Contains(".meta"))
                {
                    string fileName = Path.GetFileName(file).Split('.')[0];
                   
                    if (!_Parts.ContainsKey(fileName))
                        _Parts.Add(fileName, file);
                }
            }
        }

        //parses all colors out of ldcfgalt.ldr
        private void ParseColors()
        {
            _MainColors = new Dictionary<int, Material>();
            using (StreamReader reader = new StreamReader(_ColorConfigPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Regex regex = new Regex("[ ]{2,}", RegexOptions.None);
                    line = regex.Replace(line, " ").Trim();
                    //args split by spaces
                    var args = line.Split(' ');
                    
                    //read each line where second arg is !COLOUR
                    //example line --> "0 !COLOUR Black                              CODE   0   VALUE #05131D   EDGE #3F474C"
                    if (args.Length  > 1 && args[1] == "!COLOUR")
                    {   
                        //set up a path for the Assets/Ldraw-Importer/GeneratedMateriats/<ColorName>.mat
                        var path =_MaterialsPath + args[2] + ".mat";
                        if (File.Exists(path))
                        {   
                            //if file exists, load material and add to the Color dictionary
                            _MainColors.Add(int.Parse(args[4]), AssetDatabase.LoadAssetAtPath<Material>(path));
                        }
                        else
                        {
                            Color color;
                            //if the mat file doesn't exist yet, parse the color code out of the seventh argument
                            if (ColorUtility.TryParseHtmlString(args[6], out color))
                            {
                                //some colors have arg called ALPHA - treat those colors special and add alpha to mat
                                int alphaIndex = Array.IndexOf(args, "ALPHA");
                                var mat = new Material(alphaIndex  > 0? _DefaultTransparentMaterial : _DefaultOpaqueMaterial);
                                mat.name = args[2];

                                mat.color = alphaIndex > 0? new Color(color.r, color.g, color.b, int.Parse(args[alphaIndex + 1]) / 256f) 
                                    : color;

                                //if the color is CHROME
                                int chromeIndex = Array.IndexOf(args, "CHROME");
                                if (chromeIndex > 0)
                                {
                                    mat.SetFloat("_Metallic", .9f);
                                    mat.SetFloat("_Glossiness", .9f);
                                }

                                //if the color is METAL
                                int metallicIndex = Array.IndexOf(args, "METAL");
                                if(metallicIndex > 0)
                                {
                                    mat.SetFloat("_Metallic", .6f );
                                    mat.SetFloat("_Glossiness", .6f);
                                }

                                //if the color is LUMINANCE, set EmissionColor for glow
                                int luminanceIndex = Array.IndexOf(args, "LUMINANCE");
                                if (luminanceIndex > 0)
                                {
                                    mat.SetColor("_EmissionColor", mat.color);
                                }

                                AssetDatabase.CreateAsset(mat, path);
                                _MainColors.Add(int.Parse(args[4]), mat);
                            }
                        }
                    
                    }
                }
                AssetDatabase.SaveAssets();
            }
        }
        
        private void PrepareModels()
        {
            _ModelFileNames = new Dictionary<string, string>();
            var files = Directory.GetFiles(_ModelsPath, "*.*", SearchOption.AllDirectories);
            _Models = new Dictionary<string, string>();
            foreach (var file in files)
            {
                using (StreamReader reader = new StreamReader(file))
                {
                    string line;
                    string filename = String.Empty;

                    bool isFirst = true;
                    while ((line = reader.ReadLine()) != null)
                    {
                        Regex regex = new Regex("[ ]{2,}", RegexOptions.None);
                        line = regex.Replace(line, " ").Trim();
                        var args = line.Split(' ');
                        if (args.Length  > 1 && args[1] == "FILE")
                        {
                           
                            filename = GetFileName(args, 2);
                            if (isFirst)
                            {
                                _ModelFileNames.Add(Path.GetFileNameWithoutExtension(file), filename);
                                isFirst = false;
                            }
                            
                            if(_Models.ContainsKey(filename))
                                filename = String.Empty;
                            else
                                _Models.Add(filename, String.Empty);
                        }

                        if (!string.IsNullOrEmpty(filename))
                        {
                            _Models[filename] += line + "\n";
                        }
                    } 
                }
                
            }
        }

        public Mesh GetMesh(string name)
        {
            var path = Path.Combine(_MeshesPath, name + ".asset");
            return File.Exists(path) ? AssetDatabase.LoadAssetAtPath<Mesh>(path) : null;
        }
        public void SaveMesh(Mesh mesh)
        {
            var path = _MeshesPath;
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            path = Path.Combine(path, mesh.name + ".asset");
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
        }
        public static string GetFileName(string[] args, int filenamePos)
        {
            string name = string.Empty;
            for (int i = filenamePos; i < args.Length; i++)
            {
                name += args[i] + ' ';
            }

            return Path.GetFileNameWithoutExtension(name).ToLower();
        }
        public static string GetExtension(string[] args, int filenamePos)
        {
            string name = string.Empty;
            for (int i = filenamePos; i < args.Length; i++)
            {
                name += args[i] + ' ';
            }
         
            return Path.GetExtension(name).Trim();
        }
        private static LDrawConfig _Instance;

        public static LDrawConfig Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = AssetDatabase.LoadAssetAtPath<LDrawConfig>(ConfigPath);
                }

                return _Instance;
            }
        }

        private void OnEnable()
        {
            InitParts();
        }

        private const string ConfigPath = "Assets/LDraw-Importer/Editor/Config.asset";
        public const int DefaultMaterialCode = 16;
    }
}