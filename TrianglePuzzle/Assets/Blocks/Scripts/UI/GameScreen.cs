using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BBG.Blocks
{
	public class GameScreen : Screen
	{
		#region Inspector Variables

		[Space]

		[SerializeField] private GameArea gameArea = null;

		#endregion // Inspector Variables

		#region Public Methods
		
		public override void Initialize()
		{
			base.Initialize();

			gameArea.Initialize();

			GameEventManager.Instance.RegisterEventHandler(GameEventManager.LevelStartedEventId, OnLevelStarted);
		}

		/// <summary>
		/// Invoked when the Reset button on the GameScreen is clicked
		/// </summary>
		public void OnResetClicked()
		{
			// Reset the LevelSaveData for the active level so no shapes are placed
			GameManager.Instance.ResetActiveLevel();

			// Just re-stup the game area so all the shapes will be placed back in the shapes container
			SetupGameArea();
		}

		/// <summary>
		/// Invoked when the Hint button on the GameScreen is clicked
		/// </summary>
		public void OnHintClicked()
		{
			int shapeIndex;

			if (GameManager.Instance.TryUseHint(out shapeIndex))
			{
				gameArea.DisplayHint(shapeIndex);
			}
		}
		
		#endregion // Public Methods

		#region Private Methods
		
		private void OnLevelStarted(string eventId, object[] data)
		{
			SetupGameArea();
		}

		private void SetupGameArea()
		{
			LevelData		activeLevelData		= GameManager.Instance.ActiveLevelData;
			LevelSaveData	activeLevelSaveData	= GameManager.Instance.ActiveLevelSaveData;

			if (activeLevelData != null && activeLevelSaveData != null)
			{
				gameArea.SetupLevel(activeLevelData, activeLevelSaveData);
			}
		}
		
		#endregion // Private Methods
	}
}
