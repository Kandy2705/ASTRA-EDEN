using UnityEngine;
using Drakkar.GameUtils;

namespace Drakkar.Examples
{
	public class AnimationEvents : MonoBehaviour
	{
		public DrakkarTrail Trail;

		public void StartTrail()
		{
			if (Trail == null)
			{
				return;
			}

			Trail.Begin();
		}

		public void StopTrail()
		{
			if (Trail == null)
			{
				return;
			}

			Trail.End();
		}
	}
}