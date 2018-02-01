using Caliburn.Micro;
using SimpleDnsCrypt.Config;
using SimpleDnsCrypt.Extensions;
using SimpleDnsCrypt.Helper;
using SimpleDnsCrypt.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SimpleDnsCrypt.ViewModels
{
	public enum Tabs
	{
		MainTab,
		ResolverTab,
		AdvancedSettingsTab,
		QueryLogTab,
		DomainBlockLogTab,
		DomainBlacklistTab,
		AddressBlockLogTab,
		AddressBlacklistTab
	}

	[Export(typeof(MainViewModel))]
	public class MainViewModel : PropertyChangedBase, IShell
	{
		private readonly IWindowManager _windowManager;
		private readonly IEventAggregator _events;
		private string _windowTitle;
		private bool _showHiddenCards;
		private BindableCollection<LocalNetworkInterface> _localNetworkInterfaces =
			new BindableCollection<LocalNetworkInterface>();

		private BindableCollection<AvailableResolver> _resolvers;
		public Tabs SelectedTab { get; set; }

		private bool _isWorkingOnService;
		private bool _isResolverRunning;
		private DnscryptProxyConfiguration _dnscryptProxyConfiguration;

		private ObservableCollection<Language> _languages;
		private Language _selectedLanguage;
		private bool _isSavingConfiguration;
		private int _selectedTabIndex;

		private SettingsViewModel _settingsViewModel;
		private QueryLogViewModel _queryLogViewModel;
		private DomainBlockLogViewModel _domainBlockLogViewModel;
		private AddressBlockLogViewModel _addressBlockLogViewModel;
		private DomainBlacklistViewModel _domainBlacklistViewModel;
		private AddressBlacklistViewModel _addressBlacklistViewModel;
		private bool _isUninstallingService;
		private bool _isDnsCryptAutomaticModeEnabled;

		/// <summary>
		/// Initializes a new instance of the <see cref="MainViewModel"/> class
		/// </summary>
		/// <param name="windowManager">The window manager</param>
		/// <param name="events">The events</param>
		[ImportingConstructor]
		public MainViewModel(IWindowManager windowManager, IEventAggregator events)
		{
			_windowManager = windowManager;
			_events = events;
			_events.Subscribe(this);
			_windowTitle = $"{Global.ApplicationName} {VersionHelper.PublishVersion} {VersionHelper.PublishBuild} [dnscrypt-proxy {DnsCryptProxyManager.GetVersion()}]";
			SelectedTab = Tabs.MainTab;
			_isSavingConfiguration = false;
			_isWorkingOnService = false;

			_settingsViewModel = new SettingsViewModel(_windowManager, _events)
			{
				WindowTitle = LocalizationEx.GetUiString("settings", Thread.CurrentThread.CurrentCulture)
			};
			_settingsViewModel.PropertyChanged += SettingsViewModelOnPropertyChanged;
			_queryLogViewModel = new QueryLogViewModel(_windowManager, _events);
			_domainBlockLogViewModel = new DomainBlockLogViewModel(_windowManager, _events);
			_domainBlacklistViewModel = new DomainBlacklistViewModel(_windowManager, _events);
			_addressBlockLogViewModel = new AddressBlockLogViewModel(_windowManager, _events);
			_addressBlacklistViewModel = new AddressBlacklistViewModel(_windowManager, _events);

			_resolvers = new BindableCollection<AvailableResolver>();
			
		}

		public void Initialize()
		{
			if (DnsCryptProxyManager.IsDnsCryptProxyInstalled())
			{
				if (DnsCryptProxyManager.IsDnsCryptProxyRunning())
				{
					_isResolverRunning = true;
				}
			}

			if (DnscryptProxyConfiguration != null && (DnscryptProxyConfiguration.server_names == null || DnscryptProxyConfiguration.server_names.Count == 0))
			{
				_isDnsCryptAutomaticModeEnabled = true;
			}
			else
			{
				_isDnsCryptAutomaticModeEnabled = false;
			}
		}


		public bool IsDnsCryptAutomaticModeEnabled
		{
			get => _isDnsCryptAutomaticModeEnabled;
			set
			{
				if (value.Equals(_isDnsCryptAutomaticModeEnabled)) return;
				_isDnsCryptAutomaticModeEnabled = value;

				if (_isDnsCryptAutomaticModeEnabled)
				{

					DnscryptProxyConfiguration.server_names = null;
					SaveDnsCryptConfiguration();
					GetAvailableResolvers();
					HandleService();
				}
				else
				{
					if (DnscryptProxyConfiguration.server_names == null || DnscryptProxyConfiguration.server_names.Count == 0)
					{
						_isDnsCryptAutomaticModeEnabled = true;
						_windowManager.ShowMetroMessageBox("At least one server must be selected. Otherwise, dnscrypt-proxy uses all servers corresponding to the selected filters.", "No server selected",
							MessageBoxButton.OK, BoxType.Warning);
					}
				}
				NotifyOfPropertyChange(() => IsDnsCryptAutomaticModeEnabled);
			}
		}

		private void SettingsViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
		{
			if (propertyChangedEventArgs != null)
			{
				if (propertyChangedEventArgs.PropertyName.Equals("IsInitialized") ||
				    propertyChangedEventArgs.PropertyName.Equals("IsActive")) return;

				switch (propertyChangedEventArgs.PropertyName)
				{
					case "IsAdvancedSettingsTabVisible":
						if (!SettingsViewModel.IsAdvancedSettingsTabVisible)
						{
							if (SelectedTab == Tabs.AdvancedSettingsTab)
							{
								SelectedTabIndex = 0;
							}
						}
						break;
					case "IsQueryLogTabVisible":
						if (QueryLogViewModel.IsQueryLogLogging)
						{
							QueryLogViewModel.IsQueryLogLogging = false;
						}

						if (!SettingsViewModel.IsQueryLogTabVisible)
						{
							if (SelectedTab == Tabs.QueryLogTab)
							{
								SelectedTabIndex = 0;
							}
						}
						break;
					case "IsDomainBlockLogLogging":
						if (DomainBlockLogViewModel.IsDomainBlockLogLogging)
						{
							DomainBlockLogViewModel.IsDomainBlockLogLogging = false;
						}

						if (!SettingsViewModel.IsDomainBlockLogTabVisible)
						{
							if (SelectedTab == Tabs.DomainBlockLogTab)
							{
								SelectedTabIndex = 0;
							}
						}
						break;
					case "IsAddressBlockLogLogging":
						if (AddressBlockLogViewModel.IsAddressBlockLogLogging)
						{
							AddressBlockLogViewModel.IsAddressBlockLogLogging = false;
						}

						if (!SettingsViewModel.IsAddressBlockLogTabVisible)
						{
							if (SelectedTab == Tabs.AddressBlockLogTab)
							{
								SelectedTabIndex = 0;
							}
						}
						break;
					case "IsDomainBlacklistTabVisible":
						if (!SettingsViewModel.IsDomainBlacklistTabVisible)
						{
							if (SelectedTab == Tabs.DomainBlacklistTab)
							{
								SelectedTabIndex = 0;
							}
						}
						break;
					case "IsAddressBlacklistTabVisible":
						if (!SettingsViewModel.IsAddressBlacklistTabVisible)
						{
							if (SelectedTab == Tabs.AddressBlacklistTab)
							{
								SelectedTabIndex = 0;
							}
						}
						break;
				}
			}
		}

		public DomainBlacklistViewModel DomainBlacklistViewModel
		{
			get => _domainBlacklistViewModel;
			set
			{
				if (value.Equals(_domainBlacklistViewModel)) return;
				_domainBlacklistViewModel = value;
				NotifyOfPropertyChange(() => DomainBlacklistViewModel);
			}
		}

		public AddressBlacklistViewModel AddressBlacklistViewModel
		{
			get => _addressBlacklistViewModel;
			set
			{
				if (value.Equals(_addressBlacklistViewModel)) return;
				_addressBlacklistViewModel = value;
				NotifyOfPropertyChange(() => AddressBlacklistViewModel);
			}
		}

		public DomainBlockLogViewModel DomainBlockLogViewModel
		{
			get => _domainBlockLogViewModel;
			set
			{
				if (value.Equals(_domainBlockLogViewModel)) return;
				_domainBlockLogViewModel = value;
				NotifyOfPropertyChange(() => DomainBlockLogViewModel);
			}
		}

		public AddressBlockLogViewModel AddressBlockLogViewModel
		{
			get => _addressBlockLogViewModel;
			set
			{
				if (value.Equals(_addressBlockLogViewModel)) return;
				_addressBlockLogViewModel = value;
				NotifyOfPropertyChange(() => AddressBlockLogViewModel);
			}
		}

		public QueryLogViewModel QueryLogViewModel
		{
			get => _queryLogViewModel;
			set
			{
				if (value.Equals(_queryLogViewModel)) return;
				_queryLogViewModel = value;
				NotifyOfPropertyChange(() => QueryLogViewModel);
			}
		}

		public SettingsViewModel SettingsViewModel
		{
			get => _settingsViewModel;
			set
			{
				if (value.Equals(_settingsViewModel)) return;
				_settingsViewModel = value;
				NotifyOfPropertyChange(() => SettingsViewModel);
			}
		}

		public int SelectedTabIndex
		{
			get => _selectedTabIndex;
			set
			{
				_selectedTabIndex = value;
				NotifyOfPropertyChange(() => SelectedTabIndex);
			}
		}

		public void TabControl_SelectionChanged(SelectionChangedEventArgs selectionChangedEventArgs)
		{
			try
			{
				if (selectionChangedEventArgs?.AddedItems.Count != 1) return;
				var tabItem = (TabItem) selectionChangedEventArgs.AddedItems[0];
				if (string.IsNullOrEmpty((string) tabItem.Tag)) return;

				switch ((string)tabItem.Tag)
				{
					case "mainTab":
						SelectedTab = Tabs.MainTab;
						break;
					case "resolverTab":
						SelectedTab = Tabs.ResolverTab;
						GetAvailableResolvers();
						break;
					case "advancedSettingsTab":
						SelectedTab = Tabs.AdvancedSettingsTab;
						break;
					case "queryLogTab":
						SelectedTab = Tabs.QueryLogTab;
						break;
					case "domainBlockLogTab":
						SelectedTab = Tabs.DomainBlockLogTab;
						break;
					case "domainBlacklistTab":
						SelectedTab = Tabs.DomainBlacklistTab;
						break;
					case "addressBlockLogTab":
						SelectedTab = Tabs.AddressBlockLogTab;
						break;
					case "addressBlacklistTab":
						SelectedTab = Tabs.AddressBlacklistTab;
						break;
					default:
						SelectedTab = Tabs.MainTab;
						break;
				}
			}
			catch(Exception) { }
		}

		public void About()
		{
			var win = new AboutViewModel(_windowManager, _events)
			{
				WindowTitle = LocalizationEx.GetUiString("about", Thread.CurrentThread.CurrentCulture)
			};
			dynamic settings = new ExpandoObject();
			settings.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			_windowManager.ShowDialog(win, null, settings);
		}

		public void Settings()
		{
			dynamic settings = new ExpandoObject();
			settings.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			var result =_windowManager.ShowDialog(SettingsViewModel, null, settings);
			if (!result)
			{
				Properties.Settings.Default.Save();
			}
		}

		public DnscryptProxyConfiguration DnscryptProxyConfiguration
		{
			get => _dnscryptProxyConfiguration;
			set
			{
				if (value.Equals(_dnscryptProxyConfiguration)) return;
				_dnscryptProxyConfiguration = value;
				NotifyOfPropertyChange(() => DnscryptProxyConfiguration);
			}
		}

		public void SaveDnsCryptConfiguration()
		{
			IsSavingConfiguration = true;
			try
			{
				if (DnscryptProxyConfiguration == null) return;
				DnscryptProxyConfigurationManager.DnscryptProxyConfiguration = _dnscryptProxyConfiguration;
				if (DnscryptProxyConfigurationManager.SaveConfiguration())
				{
					DnscryptProxyConfigurationManager.LoadConfiguration();
					_dnscryptProxyConfiguration = DnscryptProxyConfigurationManager.DnscryptProxyConfiguration;
					if (DnsCryptProxyManager.IsDnsCryptProxyInstalled())
					{
						if (DnsCryptProxyManager.IsDnsCryptProxyRunning())
						{
							DnsCryptProxyManager.Restart();
						}
						else
						{
							DnsCryptProxyManager.Start();
						}
					}
				}
				_isResolverRunning = DnsCryptProxyManager.IsDnsCryptProxyRunning();
				NotifyOfPropertyChange(() => IsResolverRunning);
			}
			catch(Exception) { }
			IsSavingConfiguration = false;
		}

		/// <summary>
		///     The currently selected language.
		/// </summary>
		public Language SelectedLanguage
		{
			get => _selectedLanguage;
			set
			{
				if (value.Equals(_selectedLanguage)) return;
				_selectedLanguage = value;
				Properties.Settings.Default.PreferredLanguage = _selectedLanguage.ShortCode;
				Properties.Settings.Default.Save();
				LocalizationEx.SetCulture(_selectedLanguage.ShortCode);
				NotifyOfPropertyChange(() => SelectedLanguage);
			}
		}

		/// <summary>
		///     List of all available languages.
		/// </summary>
		public ObservableCollection<Language> Languages
		{
			get => _languages;
			set
			{
				if (value.Equals(_languages)) return;
				_languages = value;
				NotifyOfPropertyChange(() => Languages);
			}
		}

		public bool IsResolverRunning
		{
			get => _isResolverRunning;
			set
			{
				HandleService();
				NotifyOfPropertyChange(() => IsResolverRunning);
			}
		}

		public bool IsSavingConfiguration
		{
			get => _isSavingConfiguration;
			set
			{
				_isSavingConfiguration = value;
				NotifyOfPropertyChange(() => IsSavingConfiguration);
			}
		}
		

		private async void HandleService()
		{
			IsWorkingOnService = true;
			if (IsResolverRunning)
			{
				// service is running, stop it
				await Task.Run(() => { DnsCryptProxyManager.Stop(); }).ConfigureAwait(false);
				await Task.Delay(Global.ServiceStopTime).ConfigureAwait(false);
				_isResolverRunning = DnsCryptProxyManager.IsDnsCryptProxyRunning();
				NotifyOfPropertyChange(() => IsResolverRunning);
			}
			else
			{
				if (DnsCryptProxyManager.IsDnsCryptProxyInstalled())
				{
					// service is installed, just start them
					await Task.Run(() => { DnsCryptProxyManager.Start(); }).ConfigureAwait(false);
					await Task.Delay(Global.ServiceStartTime).ConfigureAwait(false);
					_isResolverRunning = DnsCryptProxyManager.IsDnsCryptProxyRunning();
					NotifyOfPropertyChange(() => IsResolverRunning);
				}
				else
				{
					//install and start the service
					await Task.Run(() => DnsCryptProxyManager.Install()).ConfigureAwait(false);
					await Task.Delay(Global.ServiceStartTime).ConfigureAwait(false);
					await Task.Run(() => { DnsCryptProxyManager.Start(); }).ConfigureAwait(false);
					await Task.Delay(Global.ServiceStartTime).ConfigureAwait(false);
					_isResolverRunning = DnsCryptProxyManager.IsDnsCryptProxyRunning();
					NotifyOfPropertyChange(() => IsResolverRunning);
				}
			}
			IsWorkingOnService = false;
		}

		public bool IsWorkingOnService
		{
			get => _isWorkingOnService;
			set
			{
				_isWorkingOnService = value;
				NotifyOfPropertyChange(() => IsWorkingOnService);
			}
		}

		public BindableCollection<LocalNetworkInterface> LocalNetworkInterfaces
		{
			get => _localNetworkInterfaces;
			set
			{
				_localNetworkInterfaces = value;
				NotifyOfPropertyChange(() => LocalNetworkInterfaces);
			}
		}

		public BindableCollection<AvailableResolver> Resolvers
		{
			get => _resolvers;
			set
			{
				_resolvers = value;
				NotifyOfPropertyChange(() => Resolvers);
			}
		}

		/// <summary>
		///		The title of the window.
		/// </summary>
		public string WindowTitle
		{
			get => _windowTitle;
			set
			{
				_windowTitle = value;
				NotifyOfPropertyChange(() => WindowTitle);
			}
		}

		/// <summary>
		///     Show or hide the filtered network cards.
		/// </summary>
		public bool ShowHiddenCards
		{
			get => _showHiddenCards;
			set
			{
				_showHiddenCards = value;
				ReloadLoadNetworkInterfaces();
				NotifyOfPropertyChange(() => ShowHiddenCards);
			}
		}

		/// <summary>
		///     Load the local network cards.
		/// </summary>
		private void ReloadLoadNetworkInterfaces()
		{

			var localNetworkInterfaces = LocalNetworkInterfaceManager.GetLocalNetworkInterfaces(
				DnscryptProxyConfigurationManager.DnscryptProxyConfiguration.listen_addresses.ToList(), ShowHiddenCards);
			_localNetworkInterfaces.Clear();

			if (localNetworkInterfaces.Count == 0) return;

			foreach (var localNetworkInterface in localNetworkInterfaces)
			{
				_localNetworkInterfaces.Add(localNetworkInterface);
			}
		}

		public async void NetworkCardClicked(LocalNetworkInterface localNetworkInterface)
		{
			if (localNetworkInterface == null) return;
			if (!localNetworkInterface.IsChangeable) return;
			localNetworkInterface.IsChangeable = false;
			if (localNetworkInterface.UseDnsCrypt)
			{
				var status = LocalNetworkInterfaceManager.UnsetNameservers(localNetworkInterface);
				localNetworkInterface.UseDnsCrypt = !status;
			}
			else
			{
				// only add the local address if the proxy is running 
				if (DnsCryptProxyManager.IsDnsCryptProxyRunning())
				{
					var status = LocalNetworkInterfaceManager.SetNameservers(localNetworkInterface,
						LocalNetworkInterfaceManager.ConvertToDnsList(
							DnscryptProxyConfigurationManager.DnscryptProxyConfiguration.listen_addresses.ToList()));
					localNetworkInterface.UseDnsCrypt = status;
				}
				else
				{
					_windowManager.ShowMetroMessageBox("You should start the DnsCrypt service first!", "Service not running",
						MessageBoxButton.OK, BoxType.Warning);
				}
			}
			await Task.Delay(1000).ConfigureAwait(false);
			localNetworkInterface.IsChangeable = true;
			ReloadLoadNetworkInterfaces();
		}

		#region Resolvers

		public void SaveLocalServers()
		{
			if (DnscryptProxyConfiguration?.server_names?.Count > 0)
			{
				IsDnsCryptAutomaticModeEnabled = false;
				SaveDnsCryptConfiguration();
				HandleService();
			}
			else
			{
				_windowManager.ShowMetroMessageBox("At least one server must be selected. Otherwise, dnscrypt-proxy uses all servers corresponding to the selected filters.", "No server selected",
					MessageBoxButton.OK, BoxType.Warning);
			}
		}

		public void ResolverClicked(AvailableResolver resolver)
		{
			if (resolver == null) return;
			if (resolver.IsInServerList)
			{
				if (DnscryptProxyConfiguration.server_names.Contains(resolver.Name))
				{
					DnscryptProxyConfiguration.server_names.Remove(resolver.Name);
				}
				resolver.IsInServerList = false;
			}
			else
			{
				if (DnscryptProxyConfiguration.server_names == null)
				{
					DnscryptProxyConfiguration.server_names = new ObservableCollection<string>();
				}
				if (!DnscryptProxyConfiguration.server_names.Contains(resolver.Name))
				{
					DnscryptProxyConfiguration.server_names.Add(resolver.Name);
				}
				resolver.IsInServerList = true;
			}
		}

		private void GetAvailableResolvers()
		{
			_resolvers.Clear();
			var resolvers = DnsCryptProxyManager.GetAvailableResolvers();
			_resolvers.AddRange(resolvers);
			
		}

		private void GetAllResolvers()
		{

			var resolvers = DnsCryptProxyManager.GetAllResolvers();

			if (resolvers != null)
			{

			}
			/*var serverNames = new List<string>();
			if (DnscryptProxyConfiguration.server_names != null && DnscryptProxyConfiguration.server_names.Count > 0)
			{
				serverNames = DnscryptProxyConfiguration.server_names.ToList();
			}

			if (DnscryptProxyConfiguration?.sources == null) return;
			var sources = DnscryptProxyConfiguration.sources;
			foreach (var source in sources)
			{
				if (string.IsNullOrEmpty(source.Value.cache_file)) continue;

				var cacheFile = source.Value.cache_file;
				var cacheFileFullPath = Path.Combine(Directory.GetCurrentDirectory(), Global.DnsCryptProxyFolder, cacheFile);
				if (!File.Exists(cacheFileFullPath)) continue;
				var content = File.ReadAllText(cacheFileFullPath);
				if (string.IsNullOrEmpty(content)) continue;
				var rawStampList = content.Split(new[] { '#', '#' }, StringSplitOptions.RemoveEmptyEntries);
				_resolvers.Clear();
				foreach (var rawStampListEntry in rawStampList)
				{
					var def = rawStampListEntry.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
					if (def.Length != 3) continue;
					var server = new Resolver
					{
						Group = source.Key,
						Name = def[0].Trim(),
						Comment = def[1].Trim(),
						Stamp = new Stamp(def[2].Trim())
					};

					if (DnscryptProxyConfiguration.require_nolog)
					{
						if (!server.Stamp.NoLog)
						{
							continue;
						}
					}

					if (DnscryptProxyConfiguration.require_dnssec)
					{
						if (!server.Stamp.DnsSec)
						{
							continue;
						}
					}

					if (!DnscryptProxyConfiguration.ipv6_servers)
					{
						if (server.Stamp.Ipv6)
						{
							continue;
						}
					}

					if (serverNames.Contains(server.Name))
					{
						server.IsInServerList = true;
					}

					_resolvers.Add(server);

				}
			}*/
		}

		#endregion

		#region Advanced Settings
		/// <summary>
		///     Uninstall the installed dnscrypt-proxy service.
		/// </summary>
		public async void UninstallService()
		{
			var result = _windowManager.ShowMetroMessageBox(
				LocalizationEx.GetUiString("dialog_message_uninstall", Thread.CurrentThread.CurrentCulture),
				LocalizationEx.GetUiString("dialog_uninstall_title", Thread.CurrentThread.CurrentCulture),
				MessageBoxButton.YesNo, BoxType.Default);

			if (result == MessageBoxResult.Yes)
			{
				IsUninstallingService = true;

				if (DnsCryptProxyManager.IsDnsCryptProxyRunning())
				{
					await Task.Run(() =>
					{
						DnsCryptProxyManager.Stop();
					}).ConfigureAwait(false);
					await Task.Delay(Global.ServiceStopTime).ConfigureAwait(false);
				}
				await Task.Run(() =>
				{
					DnsCryptProxyManager.Uninstall();
				}).ConfigureAwait(false);
				await Task.Delay(Global.ServiceUninstallTime).ConfigureAwait(false);
				_isResolverRunning = DnsCryptProxyManager.IsDnsCryptProxyRunning();
				NotifyOfPropertyChange(() => IsResolverRunning);

				// recover the network interfaces (also the hidden and down cards)
				var localNetworkInterfaces = LocalNetworkInterfaceManager.GetLocalNetworkInterfaces(
					DnscryptProxyConfigurationManager.DnscryptProxyConfiguration.listen_addresses.ToList());
				foreach (var localNetworkInterface in localNetworkInterfaces)
				{
					if (!localNetworkInterface.UseDnsCrypt) continue;
					var status = LocalNetworkInterfaceManager.SetNameservers(localNetworkInterface, new List<DnsServer>());
					var card = _localNetworkInterfaces.SingleOrDefault(n => n.Description.Equals(localNetworkInterface.Description));
					if (card != null)
					{
						card.UseDnsCrypt = !status;
					}
				}
				await Task.Delay(1000).ConfigureAwait(false);
				ReloadLoadNetworkInterfaces();
				IsUninstallingService = false;
			}
		}

		public bool IsUninstallingService
		{
			get => _isUninstallingService;
			set
			{
				_isUninstallingService = value;
				NotifyOfPropertyChange(() => IsUninstallingService);
			}
		}

		#endregion
	}
}