using System;
using System.Collections.Generic;
using UnityEngine;

namespace LDraw
{
	
	public class LDrawSubFile : LDrawCommand
	{
		private string _Name;
		private string _Extension;
		private Matrix4x4 _Matrix;
		private LDrawModel _Model;

		public void GetModelGameObject(Transform parent)
		{
			_Model.CreateMeshGameObject(_Matrix, GetMaterial(), parent);
		}

		public override void PrepareMeshData(List<int> triangles, List<Vector3> verts)
		{
			 
		}

		public override void Deserialize(string serialized)
		{
            //args are seperated by spaces
			var args = serialized.Split(' ');
            //there are 12 args after the command type and color that matter
			float[] param = new float[12];

            //get the file name and extension from the last arg in the line
			_Name = LDrawConfig.GetFileName(args, 14);
			_Extension = LDrawConfig.GetExtension(args, 14);
			for (int i = 0; i < param.Length; i++)
			{
                //offset by two to skip the command type and color
				int argNum = i + 2;
				if (!Single.TryParse(args[argNum], out param[i]))
				{
					Debug.LogError(
						String.Format(
							"Something wrong with parameters in {0} sub-file reference command. ParamNum:{1}, Value:{2}",
							_Name,
							argNum,
							args[argNum]));
				}
			}

            //create the LDrawModel by fetching the part file by name from the blueprints base parts
			_Model = LDrawModel.Create(_Name, LDrawConfig.Instance.GetSerializedPart(_Name));

            //create the TRS matrix from the parameters
            //- part location arg 0-2 goes on end of TRS matrix
            //- transformation matrix assembled in top left 3x3 of 4x4 matrix
            _Matrix = new Matrix4x4();
            _Matrix.SetRow(0, new Vector4(param[3], param[4], param[5], param[0]));
            _Matrix.SetRow(1, new Vector4(param[6], param[7], param[8], param[1]));
            _Matrix.SetRow(2, new Vector4(param[9], param[10], param[11], param[2]));
            _Matrix.SetRow(3, new Vector4(0, 0, 0,  1));
        }

		private Material GetMaterial()
		{
			if(_Extension == ".ldr") return  null;
            //fetch the material to associate with mesh by code or name
			if (_ColorCode > 0) return LDrawConfig.Instance.GetColoredMaterial(_ColorCode);
			if (_Color != null) return LDrawConfig.Instance.GetColoredMaterial(_Color);
			return LDrawConfig.Instance.GetColoredMaterial(0);
		}
		
	}

}