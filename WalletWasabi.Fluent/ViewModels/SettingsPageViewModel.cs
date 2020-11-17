using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using NBitcoin;
using Splat;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels
{
	public class SettingsPageViewModel : NavBarItemViewModel
	{
		private Network _network;
		private string _torSocks5EndPoint;
		private string _bitcoinP2pEndPoint;
		private string _localBitcoinCoreDataDir;
		private bool _autocopy;
		private bool _customFee;
		private bool _customChangeAddress;
		private bool _useTor;
		private bool _startLocalBitcoinCoreOnStartup;
		private bool _stopLocalBitcoinCoreOnShutdown;
		private bool _isModified;
		private bool _terminateTorOnExit;
		private string _somePrivacyLevel;
		private string _finePrivacyLevel;
		private string _strongPrivacyLevel;
		private string _dustThreshold;
		private FeeDisplayFormat _selectedFeeDisplayFormat;
		private bool _darkModeEnabled;

		private Global Global;

		public SettingsPageViewModel(NavigationStateViewModel navigationState) : base(navigationState, NavigationTarget.HomeScreen)
		{
			Title = "Settings";

			Global = Locator.Current.GetService<Global>();

			this.ValidateProperty(x => x.SomePrivacyLevel, ValidateSomePrivacyLevel);
			this.ValidateProperty(x => x.FinePrivacyLevel, ValidateFinePrivacyLevel);
			this.ValidateProperty(x => x.StrongPrivacyLevel, ValidateStrongPrivacyLevel);
			this.ValidateProperty(x => x.DustThreshold, ValidateDustThreshold);
			this.ValidateProperty(x => x.TorSocks5EndPoint, ValidateTorSocks5EndPoint);
			this.ValidateProperty(x => x.BitcoinP2pEndPoint, ValidateBitcoinP2pEndPoint);

			_darkModeEnabled = true;
			Autocopy = Global.UiConfig.Autocopy;
			CustomFee = Global.UiConfig.IsCustomFee;
			CustomChangeAddress = Global.UiConfig.IsCustomChangeAddress;

			var config = new Config(Global.Config.FilePath);
			config.LoadOrCreateDefaultFile();

			_network = config.Network;
			_torSocks5EndPoint = config.TorSocks5EndPoint.ToString(-1);
			UseTor = config.UseTor;
			TerminateTorOnExit = config.TerminateTorOnExit;
			StartLocalBitcoinCoreOnStartup = config.StartLocalBitcoinCoreOnStartup;
			StopLocalBitcoinCoreOnShutdown = config.StopLocalBitcoinCoreOnShutdown;

			_somePrivacyLevel = config.PrivacyLevelSome.ToString();
			_finePrivacyLevel = config.PrivacyLevelFine.ToString();
			_strongPrivacyLevel = config.PrivacyLevelStrong.ToString();

			_dustThreshold = config.DustThreshold.ToString();

			_bitcoinP2pEndPoint = config.GetP2PEndpoint().ToString(defaultPort: -1);
			_localBitcoinCoreDataDir = config.LocalBitcoinCoreDataDir;

			IsModified = !Global.Config.AreDeepEqual(config);

			this.WhenAnyValue(
				x => x.Network,
				x => x.UseTor,
				x => x.TerminateTorOnExit,
				x => x.StartLocalBitcoinCoreOnStartup,
				x => x.StopLocalBitcoinCoreOnShutdown)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(_ => Save());

			this.WhenAnyValue(x => x.Autocopy)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(x => Global.UiConfig.Autocopy = x);

			this.WhenAnyValue(x => x.CustomFee)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(x => Global.UiConfig.IsCustomFee = x);

			this.WhenAnyValue(x => x.CustomChangeAddress)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(x => Global.UiConfig.IsCustomChangeAddress = x);

			OpenConfigFileCommand = ReactiveCommand.CreateFromTask(OpenConfigFileAsync);

			TextBoxLostFocusCommand = ReactiveCommand.Create(Save);

			Observable
				.Merge(OpenConfigFileCommand.ThrownExceptions)
				.Merge(TextBoxLostFocusCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));

			SelectedFeeDisplayFormat = Enum.IsDefined(typeof(FeeDisplayFormat), Global.UiConfig.FeeDisplayFormat)
				? (FeeDisplayFormat)Global.UiConfig.FeeDisplayFormat
				: FeeDisplayFormat.SatoshiPerByte;

			this.WhenAnyValue(x => x.SelectedFeeDisplayFormat)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => Global.UiConfig.FeeDisplayFormat = (int)x);

			this.WhenAnyValue(x => x.DarkModeEnabled)
				.Skip(1)
				.Subscribe(
					x =>
					{
						var currentTheme = Application.Current.Styles.Select(x => (StyleInclude)x).FirstOrDefault(x => x.Source is { } && x.Source.AbsolutePath.Contains("Themes"));

						if (currentTheme?.Source is { } src)
						{
							var themeIndex = Application.Current.Styles.IndexOf(currentTheme);

							var newTheme = new StyleInclude(new Uri("avares://WalletWasabi.Fluent/App.xaml"))
							{
								Source = new Uri($"avares://WalletWasabi.Fluent/Styles/Themes/{(src.AbsolutePath.Contains("Light") ? "BaseDark" : "BaseLight")}.xaml")
							};

							Application.Current.Styles[themeIndex] = newTheme;
						}
					});
		}

		private bool TabOpened { get; set; }

		private object ConfigLock { get; } = new object();

		public ReactiveCommand<Unit, Unit> OpenConfigFileCommand { get; }

		public ReactiveCommand<Unit, Unit> TextBoxLostFocusCommand { get; }

		public Version BitcoinCoreVersion => Constants.BitcoinCoreVersion;

		public IEnumerable<Network> Networks => new[]
		{
			Network.Main,
			Network.TestNet,
			Network.RegTest
		};

		public bool DarkModeEnabled
		{
			get => _darkModeEnabled;
			set => this.RaiseAndSetIfChanged(ref _darkModeEnabled, value);
		}

		public Network Network
		{
			get => _network;
			set => this.RaiseAndSetIfChanged(ref _network, value);
		}

		public string TorSocks5EndPoint
		{
			get => _torSocks5EndPoint;
			set => this.RaiseAndSetIfChanged(ref _torSocks5EndPoint, value);
		}

		public string BitcoinP2pEndPoint
		{
			get => _bitcoinP2pEndPoint;
			set => this.RaiseAndSetIfChanged(ref _bitcoinP2pEndPoint, value);
		}

		public string LocalBitcoinCoreDataDir
		{
			get => _localBitcoinCoreDataDir;
			set => this.RaiseAndSetIfChanged(ref _localBitcoinCoreDataDir, value);
		}

		public bool IsModified
		{
			get => _isModified;
			set => this.RaiseAndSetIfChanged(ref _isModified, value);
		}

		public bool Autocopy
		{
			get => _autocopy;
			set => this.RaiseAndSetIfChanged(ref _autocopy, value);
		}

		public bool CustomFee
		{
			get => _customFee;
			set => this.RaiseAndSetIfChanged(ref _customFee, value);
		}

		public bool CustomChangeAddress
		{
			get => _customChangeAddress;
			set => this.RaiseAndSetIfChanged(ref _customChangeAddress, value);
		}

		public bool StartLocalBitcoinCoreOnStartup
		{
			get => _startLocalBitcoinCoreOnStartup;
			set => this.RaiseAndSetIfChanged(ref _startLocalBitcoinCoreOnStartup, value);
		}

		public bool StopLocalBitcoinCoreOnShutdown
		{
			get => _stopLocalBitcoinCoreOnShutdown;
			set => this.RaiseAndSetIfChanged(ref _stopLocalBitcoinCoreOnShutdown, value);
		}

		public bool UseTor
		{
			get => _useTor;
			set => this.RaiseAndSetIfChanged(ref _useTor, value);
		}

		public bool TerminateTorOnExit
		{
			get => _terminateTorOnExit;
			set => this.RaiseAndSetIfChanged(ref _terminateTorOnExit, value);
		}

		public string SomePrivacyLevel
		{
			get => _somePrivacyLevel;
			set => this.RaiseAndSetIfChanged(ref _somePrivacyLevel, value);
		}

		public string FinePrivacyLevel
		{
			get => _finePrivacyLevel;
			set => this.RaiseAndSetIfChanged(ref _finePrivacyLevel, value);
		}

		public string StrongPrivacyLevel
		{
			get => _strongPrivacyLevel;
			set => this.RaiseAndSetIfChanged(ref _strongPrivacyLevel, value);
		}

		public string DustThreshold
		{
			get => _dustThreshold;
			set => this.RaiseAndSetIfChanged(ref _dustThreshold, value);
		}

		public IEnumerable<FeeDisplayFormat> FeeDisplayFormats => Enum.GetValues(typeof(FeeDisplayFormat)).Cast<FeeDisplayFormat>();

		public FeeDisplayFormat SelectedFeeDisplayFormat
		{
			get => _selectedFeeDisplayFormat;
			set => this.RaiseAndSetIfChanged(ref _selectedFeeDisplayFormat, value);
		}

		public override string IconName => "settings_regular";

		private void Save()
		{
			// While the Tab is opening we are setting properties with loading and also LostFocus command called by Avalonia
			// Those would trigger the Save function before we load the config.
			if (!TabOpened)
			{
				return;
			}

			var network = Network;
			if (network is null)
			{
				return;
			}

			if (Validations.Any)
			{
				return;
			}

			var config = new Config(Global.Config.FilePath);

			Dispatcher.UIThread.PostLogException(() =>
			{
				lock (ConfigLock)
				{
					config.LoadFile();
					if (Network == config.Network)
					{
						if (EndPointParser.TryParse(TorSocks5EndPoint, Constants.DefaultTorSocksPort, out EndPoint torEp))
						{
							config.TorSocks5EndPoint = torEp;
						}
						if (EndPointParser.TryParse(BitcoinP2pEndPoint, network.DefaultPort, out EndPoint p2pEp))
						{
							config.SetP2PEndpoint(p2pEp);
						}
						config.UseTor = UseTor;
						config.TerminateTorOnExit = TerminateTorOnExit;
						config.StartLocalBitcoinCoreOnStartup = StartLocalBitcoinCoreOnStartup;
						config.StopLocalBitcoinCoreOnShutdown = StopLocalBitcoinCoreOnShutdown;
						config.LocalBitcoinCoreDataDir = Guard.Correct(LocalBitcoinCoreDataDir);
						config.DustThreshold = decimal.TryParse(DustThreshold, out var threshold) ? Money.Coins(threshold) : Config.DefaultDustThreshold;
						config.PrivacyLevelSome = int.TryParse(SomePrivacyLevel, out int level) ? level : Config.DefaultPrivacyLevelSome;
						config.PrivacyLevelStrong = int.TryParse(StrongPrivacyLevel, out level) ? level : Config.DefaultPrivacyLevelStrong;
						config.PrivacyLevelFine = int.TryParse(FinePrivacyLevel, out level) ? level : Config.DefaultPrivacyLevelFine;
					}
					else
					{
						config.Network = Network;
						BitcoinP2pEndPoint = config.GetP2PEndpoint().ToString(defaultPort: -1);
					}
					config.ToFile();
					IsModified = !Global.Config.AreDeepEqual(config);
				}
			});
		}

		private async Task OpenConfigFileAsync()
		{
			await FileHelpers.OpenFileInTextEditorAsync(Global.Config.FilePath);
		}

		private void ValidateSomePrivacyLevel(IValidationErrors errors)
			=> ValidatePrivacyLevel(errors, SomePrivacyLevel, whiteSpaceOk: true);

		private void ValidateFinePrivacyLevel(IValidationErrors errors)
			=> ValidatePrivacyLevel(errors, FinePrivacyLevel, whiteSpaceOk: true);

		private void ValidateStrongPrivacyLevel(IValidationErrors errors)
			=> ValidatePrivacyLevel(errors, StrongPrivacyLevel, whiteSpaceOk: true);

		private void ValidateDustThreshold(IValidationErrors errors)
			=> ValidateDustThreshold(errors, DustThreshold, whiteSpaceOk: true);

		private void ValidateTorSocks5EndPoint(IValidationErrors errors)
			=> ValidateEndPoint(errors, TorSocks5EndPoint, Constants.DefaultTorSocksPort, whiteSpaceOk: true);

		private void ValidateBitcoinP2pEndPoint(IValidationErrors errors)
			=> ValidateEndPoint(errors, BitcoinP2pEndPoint, Network.DefaultPort, whiteSpaceOk: true);

		private void ValidatePrivacyLevel(IValidationErrors errors, string value, bool whiteSpaceOk)
		{
			if (!whiteSpaceOk || !string.IsNullOrWhiteSpace(value))
			{
				if (!uint.TryParse(value, out _))
				{
					errors.Add(ErrorSeverity.Error, "Invalid privacy level.");
				}
			}
		}

		private void ValidateDustThreshold(IValidationErrors errors, string dustThreshold, bool whiteSpaceOk)
		{
			if (!whiteSpaceOk || !string.IsNullOrWhiteSpace(dustThreshold))
			{
				if (!string.IsNullOrEmpty(dustThreshold) && dustThreshold.Contains(',', StringComparison.InvariantCultureIgnoreCase))
				{
					errors.Add(ErrorSeverity.Error, "Use decimal point instead of comma.");
				}

				if (!decimal.TryParse(dustThreshold, out var dust) || dust < 0)
				{
					errors.Add(ErrorSeverity.Error, "Invalid dust threshold.");
				}
			}
		}

		private void ValidateEndPoint(IValidationErrors errors, string endPoint, int defaultPort, bool whiteSpaceOk)
		{
			if (!whiteSpaceOk || !string.IsNullOrWhiteSpace(endPoint))
			{
				if (!EndPointParser.TryParse(endPoint, defaultPort, out _))
				{
					errors.Add(ErrorSeverity.Error, "Invalid endpoint.");
				}
			}
		}
	}
}