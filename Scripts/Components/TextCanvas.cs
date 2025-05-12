using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teleport
{
	//! A streamable component to display text on a canvas in 3D space.
	public class TextCanvas : MonoBehaviour
	{
		void EmitChanged()
		{
			GeometrySource.GetGeometrySource().ExtractTextCanvas(this);
			Monitor.Instance.ComponentChanged(this);
		}
		public Font font;
		[SerializeField]
		string _text="";
		[SerializeField]
		float _lineHeight=0.1f;
		public int size=64;
		[SerializeField]
		Color _colour=new Color(1.0f,1.0f,1.0f,1.0f);
		public string text
		{
			get
			{
				return _text;
			}
			set
			{
				if (_text != value)
				{
					_text = value;
					EmitChanged();
				}
			}
		}
		public float lineHeight
		{
			get
			{
				return _lineHeight;
			}
			set
			{
				if (_lineHeight != value)
				{
					_lineHeight = value;
					EmitChanged();
				}
			}
		}
		public Color colour
		{
			get
			{
				return _colour;
			}
			set
			{
				if (_colour != value)
				{
					_colour = value;
					EmitChanged();
				}
			}
		}
	}
}