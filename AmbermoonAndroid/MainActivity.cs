using Ambermoon;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Silk.NET.Windowing.Sdl.Android;

namespace AmbermoonAndroid
{
    [Activity(Label = "@string/app_name", MainLauncher = true, ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape)]
    public class MainActivity : SilkActivity, GestureDetector.IOnGestureListener
    {
		private const int RequestBluetoothPermissionsId = 1001;
		private GameWindow gameWindow;
        private MusicManager musicManager;
        private GestureDetector gestureDetector;
		private InputMethodManager imm;
		private EditText hiddenEditText;
		private string lastInputText = "";

		public override bool DispatchTouchEvent(MotionEvent e)
		{
			switch (e.Action)
			{
				/*case MotionEventActions.Down:
					HandleTouchDown(e.GetX(), e.GetY());
					break;*/
				case MotionEventActions.Move:
					if (e.PointerCount == 1)
						HandleTouchMove(e.GetX(), e.GetY());
					break;
				case MotionEventActions.Up:
					if (e.PointerCount == 1)
						HandleTouchUp(e.GetX(), e.GetY());
					break;
			}

			if (gestureDetector != null)
			{
				gestureDetector.OnTouchEvent(e);
			}
			return base.DispatchTouchEvent(e);
		}

		public override bool OnTouchEvent(MotionEvent e)
        {
			if (gestureDetector != null)
			{
				gestureDetector.OnTouchEvent(e);
				return true;
			}
			return base.OnTouchEvent(e);
		}

        private void NameResetHandler()
        {
			RunOnUiThread(() => Title = "Ambermoon");
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
            musicManager?.Stop();
		}

		protected override void OnCreate(Bundle savedInstanceState)
		{
            string version;
            
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
			{
#pragma warning disable CA1416 // Validate platform compatibility
				version = PackageManager?.GetPackageInfo(new VersionedPackage(PackageName, 0), PackageManager.PackageInfoFlags.Of(0)).VersionName ?? "1.0";
#pragma warning restore CA1416 // Validate platform compatibility
			}
			else
			{
#pragma warning disable CS0618 // Type or member is obsolete
				version = PackageManager?.GetPackageInfo(PackageName, 0).VersionName ?? "1.0";
#pragma warning restore CS0618 // Type or member is obsolete
			}

			gameWindow = new($"Ambermoon.net V{version}", (keyboardRequested, text) =>
            {
                if (keyboardRequested)
                    ShowKeyboard(text);
                else
                    HideKeyboard();
            });

			ActionBar?.Hide();
			Title = "Ambermoon";

			RequestBluetoothPermissions();

			base.OnCreate(savedInstanceState);

			gestureDetector = new GestureDetector(this, this);
		}

		private void RequestBluetoothPermissions()
		{
			if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
			{
				if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.BluetoothScan) != Permission.Granted ||
					ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.BluetoothConnect) != Permission.Granted ||
					ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.BluetoothAdvertise) != Permission.Granted)
				{
					ActivityCompat.RequestPermissions(this, new string[]
					{
						Android.Manifest.Permission.BluetoothScan,
						Android.Manifest.Permission.BluetoothConnect,
						Android.Manifest.Permission.BluetoothAdvertise,
					}, RequestBluetoothPermissionsId);
				}
			}
		}

		public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
		{
			base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

			if (requestCode == RequestBluetoothPermissionsId)
			{
				// Check if all permissions were granted
				if (grantResults.Length > 0 && grantResults.All(result => result == Permission.Granted))
				{
					// All required permissions were granted
					Toast.MakeText(this, "Bluetooth permissions granted", ToastLength.Short).Show();
				}
				else
				{
					// Permissions denied
					Toast.MakeText(this, "Bluetooth permissions denied", ToastLength.Short).Show();
				}
			}
		}

		public void ShowKeyboard(string text)
		{
			RunOnUiThread(() =>
			{
				hiddenEditText.Visibility = ViewStates.Visible;
				hiddenEditText.Focusable = true;
				hiddenEditText.FocusableInTouchMode = true;
				hiddenEditText.Text = lastInputText = text;
				hiddenEditText.RequestFocus();
				hiddenEditText.SetSelection(hiddenEditText.Text.Length);
				hiddenEditText.SetCursorVisible(false);
				imm.ShowSoftInput(hiddenEditText, ShowFlags.Forced);
				hiddenEditText.Focusable = false;
				hiddenEditText.FocusableInTouchMode = false;
			});
		}

		public void HideKeyboard()
		{
			RunOnUiThread(() =>
			{
				imm.HideSoftInputFromWindow(hiddenEditText.WindowToken, 0);
				hiddenEditText.ClearFocus();
				hiddenEditText.Text = lastInputText = "";
				hiddenEditText.Visibility = ViewStates.Invisible;
			});
		}

		private void OnAfterInit()
		{
			RunOnUiThread(() =>
			{
				/*Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
					SystemUiFlags.LayoutStable |
					SystemUiFlags.LayoutHideNavigation |
					SystemUiFlags.LayoutFullscreen |
					SystemUiFlags.HideNavigation |
					SystemUiFlags.Fullscreen |
					SystemUiFlags.ImmersiveSticky);*/

				imm = (InputMethodManager)GetSystemService(InputMethodService);

				hiddenEditText = new(this);
				hiddenEditText.LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
				hiddenEditText.Text = "";
				hiddenEditText.SetBackgroundColor(Android.Graphics.Color.Transparent);
				hiddenEditText.SetTextColor(Android.Graphics.Color.Transparent);
				hiddenEditText.Visibility = ViewStates.Invisible;
				hiddenEditText.Focusable = false;
				hiddenEditText.FocusableInTouchMode = false;
				hiddenEditText.Clickable = false;
				hiddenEditText.LongClickable = false;
				hiddenEditText.CustomSelectionActionModeCallback = new NoTextSelection();

				hiddenEditText.AfterTextChanged += (sender, e) =>
				{
					string inputText = hiddenEditText.Text;

					if (inputText == lastInputText)
						return;

					int i;
					
					for (i = 0; i < inputText.Length; i++)
					{
						if (i >= lastInputText.Length || inputText[i] != lastInputText[i])
							break;
					}

					// i is now at the end of the same start of the input.

					// If i equals last input length, some chars were added -> add them
					if (i == lastInputText.Length)
					{
						for (; i < inputText.Length; i++)
							gameWindow.OnKeyChar(inputText[i]);
					}
					// If i equals current input length, some chars were removed -> remove them
					else if (i == inputText.Length)
					{
						for (; i < lastInputText.Length; i++)
							gameWindow.OnKeyDown(Key.Backspace);
					}
					// Otherwise they differ
					else
					{
						// First remove everything remaining from last input
						int j = i;
						for (; i < lastInputText.Length; i++)
							gameWindow.OnKeyDown(Key.Backspace);
						// Then add everything from new input
						for (; j < inputText.Length; j++)
							gameWindow.OnKeyChar(inputText[j]);
					}

					lastInputText = inputText;
				};

				var sdlViewGroup = FindViewById<ViewGroup>(Android.Resource.Id.Content);

				sdlViewGroup.AddView(hiddenEditText);
			});
		}

		private class NoTextSelection : Java.Lang.Object, ActionMode.ICallback
		{
			public bool OnActionItemClicked(ActionMode mode, IMenuItem item) => false;
			public bool OnCreateActionMode(ActionMode mode, IMenu menu) => false;
			public bool OnPrepareActionMode(ActionMode mode, IMenu menu) => false;
			public void OnDestroyActionMode(ActionMode mode) { }
		}

		protected override void OnRun()
        {
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;           

            FileProvider.Initialize(this);

            //appDataDir = ApplicationContext!.FilesDir!.AbsolutePath;

            var configuration = LoadConfig();
            configuration.SaveRequested += () => SaveConfig(configuration);

            try
            {
                musicManager = new MusicManager(this);
                gameWindow.Run(configuration, musicManager, NameResetHandler, OnAfterInit);
            }
            catch (Exception ex)
            {
                PrintException(ex);
            }
            finally
            {
                SaveConfig(configuration);
            }
        }

        Configuration LoadConfig()
        {
            return Configuration.Load(new Configuration { FirstStart = true });
        }

        void SaveConfig(Configuration configuration)
        {
            try
            {
                configuration.Save();
            }
            catch
            {
                Console.WriteLine("Unable to save configuration.");
            }
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                PrintException(ex);
            else
                Console.WriteLine(e.ExceptionObject?.ToString() ?? "Unhandled exception without exception object");
        }

        static void PrintException(Exception ex)
        {
            string message = ex.Message;

            if (ex.InnerException != null)
            {
                message += System.Environment.NewLine + ex.InnerException.Message;
                ex = ex.InnerException;
            }

            Console.WriteLine(message + System.Environment.NewLine + ex.StackTrace);
        }

        public bool OnDown(MotionEvent e)
        {
            // TODO
            return false;
        }

        public bool OnFling(MotionEvent e1, MotionEvent e2, float velocityX, float velocityY)
        {
            // TODO
            return false;
        }

        public void OnLongPress(MotionEvent e)
        {
            if (e.PointerCount == 1)
            {
                var coords = new MotionEvent.PointerCoords();
                e.GetPointerCoords(0, coords);
                var position = new Position(Util.Round(coords.X), Util.Round(coords.Y));
				gameWindow.OnLongPress(position);
            }
        }

		private const int SCROLL_DELAY = 100; // 100 ms
		private double lastScrollTime = 0;

		public bool OnScroll(MotionEvent e1, MotionEvent e2, float distanceX, float distanceY)
        {
			var now = DateTime.Now.TimeOfDay.TotalMilliseconds;

			if (now - lastScrollTime < SCROLL_DELAY)
				return true;

			// scroll distance should be 10 virtual pixels minimum
			float widthThreshold = 10.0f * gameWindow.Width / Global.VirtualScreenWidth;
			float heightThreshold = 10.0f * gameWindow.Height / Global.VirtualScreenHeight;
			if (Math.Abs(distanceX) < widthThreshold)
				distanceX = 0;
			if (Math.Abs(distanceY) < heightThreshold)
				distanceY = 0;
			if (distanceX == 0 && distanceY == 0)
				return true;
			var coords = new MotionEvent.PointerCoords();
            e2.GetPointerCoords(0, coords);
            var position = new Position(Util.Round(coords.X), Util.Round(coords.Y));
			gameWindow.OnMouseScroll(position, Math.Sign(distanceY), Math.Sign(distanceX));

			lastScrollTime = now;

			return true;
        }

        public void OnShowPress(MotionEvent e)
        {
            // TODO
        }

        public bool OnSingleTapUp(MotionEvent e)
        {
            if (e.PointerCount == 1)
            {
                var coords = new MotionEvent.PointerCoords();
                e.GetPointerCoords(0, coords);
                var position = new Position(Util.Round(coords.X), Util.Round(coords.Y));
                gameWindow.OnMouseDown(position, MouseButtons.Left);
                gameWindow.OnMouseUp(position, MouseButtons.Left);
                return true;
            }

            return false;
        }

		private void HandleTouchMove(float x, float y)
		{
			var position = new Position(Util.Round(x), Util.Round(y));
			gameWindow.OnFingerMoveTo(position);
		}

		private void HandleTouchUp(float x, float y)
		{
			var position = new Position(Util.Round(x), Util.Round(y));
			gameWindow.OnFingerUp(position);
		}
	}
}