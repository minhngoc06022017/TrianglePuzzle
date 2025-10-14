using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BBG.Blocks
{ 
	public class GameEventManager : EventManager<GameEventManager>
	{
		#region Member Variables

		// Event Ids
		public const string BundleSelectedEventId		= "BundleSelected";
		public const string PackSelectedEventId			= "PackSelected";
		public const string ActiveLevelCompletedEventId	= "ActiveLevelCompleted";
		public const string LevelStartedEventId			= "LevelStarted";

		// Event Id data types
		private static readonly Dictionary<string, List<System.Type>> eventDataTypes = new Dictionary<string, List<System.Type>>()
		{
			{ BundleSelectedEventId, new List<System.Type>() { typeof(BundleInfo) } },
			{ PackSelectedEventId, new List<System.Type>() { typeof(PackInfo) } },
			{ ActiveLevelCompletedEventId, new List<System.Type>() {} },
			{ LevelStartedEventId, new List<System.Type>() {} }
		};

		#endregion

		#region Protected Methods

		protected override Dictionary<string, List<Type>> GetEventDataTypes()
		{
			return eventDataTypes;
		}

		#endregion
	}
}
