using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BBG
{
	public class CellPos
	{
		public int x;
		public int y;

		public CellPos(int x, int y)
		{
			this.x = x;
			this.y = y;
		}

		public override string ToString()
		{
			return string.Format("[{0},{1}]", x, y);
		}

		public override bool Equals(object obj)
		{
			CellPos cellPos;

			if (obj == null || (cellPos = obj as CellPos) == null)
			{
				return false;
			}

			return x == cellPos.x && y == cellPos.y;
		}
		
		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public CellPos Copy()
		{
			return new CellPos(x, y);
		}
	}
}
