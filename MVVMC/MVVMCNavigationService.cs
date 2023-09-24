using MVVMC.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace MVVMC
{
    public delegate void NavigationOccuredEventArgs(string controllerId, string previousPage, string newPage);
    internal sealed class MVVMCNavigationService : INavigationService, INavigationExecutor
    {
        #region Singleton
        private static MVVMCNavigationService _instance;
        private static object _lockInstance = new object();
        public static MVVMCNavigationService GetInstance()
        {
            if (_instance != null) return _instance;
            lock (_lockInstance)
            {
                if (_instance != null) return _instance;
                _instance = new MVVMCNavigationService();
                return _instance;
            }

        }
        #endregion Singleton

        private List<Type> _controllerTypes;
        private List<Type> _viewModelTypes;
        private List<Type> _viewTypes;

        List<Region> _regions = new List<Region>();
        List<Controller> _controllers = new List<Controller>();
        private Dispatcher _dispatcher;

        internal event Action<string> ControllerCreated;
        public event Action<string> CanGoBackChangedEvent;
        public event Action<string> CanGoForwardChangedEvent;

        public event NavigationOccuredEventArgs NavigationOccured;

        private List<WeakReference<GoBackCommand>> _goBackCommands = new List<WeakReference<GoBackCommand>>();
        private List<WeakReference<GoForwardCommand>> _goForwardCommands = new List<WeakReference<GoForwardCommand>>();

        private MVVMCNavigationService()
        {
            Initialize();
        }

        private void Initialize()
        {
            //現在のアセンブリを取得
            Assembly assembly = Assembly.GetCallingAssembly();

            //アセンブリから読み込める型をすべて取得
            var assemblyTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(GetLoadableTypes).ToList();

            //MVVMC.Controllerクラスを継承しているすべてのコントローラの型情報を取得する
            _controllerTypes = assemblyTypes.Where(t => t.BaseType?.FullName == "MVVMC.Controller").ToList();

            //MVVMC.MVVMCViewModelクラスを継承しているすべてのViewModelの型情報を取得する
            _viewModelTypes =
                assemblyTypes.Where(t => HasBaseType(t, "MVVMC.MVVMCViewModel"))
                             .ToList();

            //MVVMC.Controllerクラスを継承しているコントローラが属する名前空間を取得する
            var controllerNamespaces = _controllerTypes.Select(vm => vm.Namespace);

            //Viewの型情報を取得する。
            //・ビューの名前が「View」で終わっているか
            //・型情報の名前空間がcontrollerNamespacesに含まれているか
            //上記2点を両方とも満たすことを基準に取得する
            _viewTypes = assemblyTypes.Where(
                t => controllerNamespaces.Contains(t.Namespace) &&
                t.Name.EndsWith("View", StringComparison.InvariantCultureIgnoreCase)).ToList();

        }

        /// <summary>
        /// 第一引数に指定した型が、第二引数のサブクラスか確認する。
        /// 無限ループを防ぐために10階層まで探索する。
        /// </summary>
        /// <param name="type">指定する型</param>
        /// <param name="baseTypeStr">どのクラスに継承されているかの型</param>
        /// <returns>true:第二引数のクラスを継承している。false:第二引数のクラスを継承していない</returns>
        private bool HasBaseType(Type type, string baseTypeStr)
        {
            int count = 0;
            while(type.BaseType != null && count++ < 10)
            {
                if (type.BaseType != null && type.BaseType?.FullName != null &&
                    type.BaseType.FullName.StartsWith(baseTypeStr))
                    return true;
                type = type.BaseType;
            }
            return false;
        }

        /// <summary>
        /// from: https://haacked.com/archive/2012/07/23/get-all-types-in-an-assembly.aspx/
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            try
            {
                return assembly.GetTypes();
            }
            catch(ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        /// <summary>
        /// 与えられたコントローラの型情報から、コントローラ名を返す
        /// </summary>
        /// <param name="controllerType">コントローラの型</param>
        /// <returns>コントローラ名</returns>
        /// <exception cref="Exception">コントローラクラスは、必ず名前の最後に「Controller」をつけなければ例外を出す</exception>
        private string GetControllerName(Type controllerType)
        {

            if (!controllerType.Name.EndsWith("Controller"))
                //引数で受け取ったController型名の名前が「Controller」で終わっていなかった場合。つまり命名規則に反している場合。
                throw new Exception($"Please change the name of '{controllerType.Name}'. All controllers must end with 'Controller' postfix.");

            //最後の「Controller」を取り除いたコントローラの名前を返す
            return controllerType.Name.Substring(0, controllerType.Name.Length - ("Controller".Length));
        }

        public void AddRegion(Region navArea)
        {
            _regions.Add(navArea);
        }

        internal void RemoveRegion(Region navigationArea)
        {
            _regions.Remove(navigationArea);
        }

        /// <summary>
        /// コントローラを生成し、MVVMCフレームワーク内のコントローラリストに追加する
        /// </summary>
        /// <param name="controllerID"></param>
        /// <param name="historyMode"></param>
        internal void CreateAndAddController(string controllerID, HistoryMode? historyMode)
        {
            Type type = GetControllerTypeById(controllerID);

            //取得したコントローラの型をもとにインスタンスを生成する
            var instance = Activator.CreateInstance(type);

            //生成されたインスタンスをControllerクラスへキャストする
            var controller = instance as Controller;

            //コントローラのIDを設定
            controller.ID = controllerID;

            //コントローラのNavigationServiceとExecutorを設定する
            controller.SetNavigationService(this);
            controller.NavigationExecutor = this;
            if (historyMode != null)
            {
                controller.HistoryMode = historyMode.Value;
            }

            //コントローラリストにい追加
            _controllers.Add(controller);

            //ControllerCreatedイベントを通知する
            ControllerCreated?.Invoke(controllerID);
        }

        /// <summary>
        /// ControllerIDからControllerの型を取得する
        /// </summary>
        /// <param name="controllerID">ControllerID</param>
        /// <returns>Controllerの型</returns>
        private Type GetControllerTypeById(string controllerID)
        {
            return _controllerTypes.First(c =>
                GetControllerName(c).Equals(controllerID, StringComparison.CurrentCultureIgnoreCase));
        }

        /// <summary>
        /// ControllerIDからControllerを削除する
        /// </summary>
        /// <param name="controllerID">ControllerID</param>
        internal void RemoveController(string controllerID)
        {
            var c = _controllers.First(elem => elem.ID == controllerID);
            _controllers.Remove(c);
        }

        /// <summary>
        /// ControllerIDから現在のViewModelを取得する
        /// </summary>
        /// <param name="controllerID"></param>
        /// <returns></returns>
        public MVVMCViewModel GetCurrentViewModelByControllerID(string controllerID)
        {
            var view = GetViewByControllerID(controllerID);
            if (view == null) return null;
            var fe = view as FrameworkElement;
            var vm = fe?.DataContext;
            if (vm == null) return null;
            return vm as MVVMCViewModel;
        }

        /// <summary>
        /// ControllerIDから現在のページ名を取得する
        /// </summary>
        /// <param name="controllerID"></param>
        /// <returns></returns>
        public string GetCurrentPageNameByControllerID(string controllerID)
        {
            var view = GetCurrentViewModelByControllerID(controllerID);
            if (view == null) return null;
            var name = view.GetType().Name.Replace("View", "").Replace("view", "");
            return name;
        }

        /// <summary>
        /// ControllerIDからViewを取得する
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        private object GetViewByControllerID(string ID)
        {
            var navArea = FindRegionByID(ID);
            return navArea?.Content;
        }

        /// <summary>
        /// 指定されたViewModelの型を使用して画面遷移を行う。
        /// </summary>
        /// <typeparam name="TViewModel">遷移先のViewModelの型</typeparam>
        /// <param name="parameter">遷移先に渡すパラメータ</param>
        public void NavigateWithController<TViewModel>(object parameter) where TViewModel : MVVMCViewModel
        {
            //ViewModelの名前空間を取得
            var ns = typeof(TViewModel).Namespace;

            //ViewModelの名前を取得
            var name = typeof(TViewModel).Name.Replace("ViewModel", "").Replace("View", "");


            var controllerID = ns.Split('.').Last();
            var controller = GetController(controllerID);

            //画面遷移する
            controller.Navigate(name, parameter);
        }

        /// <summary>
        /// 引数に指定されたコントローラのインスタンスを取得する。
        /// </summary>
        /// <param name="controllerID">ControllerID</param>
        /// <returns>インスタンス</returns>
        public Controller GetController(string controllerID)
        {
            return _controllers.First(elem => elem.ID.Equals(controllerID, StringComparison.CurrentCultureIgnoreCase));
        }

        /// <summary>
        /// 引数に指定されたコントローラ名から、コントローラが存在するか否か判定する
        /// </summary>
        /// <param name="controllerID">ControllerID</param>
        /// <returns>true;コントローラはすでに存在する, false:コントローラは存在しない</returns>
        public bool IsControllerExists(string controllerID)
        {
            return _controllers.Any(elem => elem.ID.Equals(controllerID, StringComparison.CurrentCultureIgnoreCase));
        }

        /// <summary>
        /// 引数に指定されたコントローラの型情報から、コントローラが存在するか否か判断する
        /// </summary>
        /// <typeparam name="TControllerType">Controllerの型</typeparam>
        /// <returns>true:コントローラが存在する、false:コントローラが存在しない</returns>
        public bool IsControllerExists<TControllerType>() where TControllerType : Controller
        {
            return IsControllerExists(GetControllerId<TControllerType>());
        }

        /// <summary>
        /// 指定したコントローラの型情報から、コントローラIDを取得する
        /// </summary>
        /// <typeparam name="TControllerType">Controllerの型</typeparam>
        /// <returns>コントローラID</returns>
        /// <exception cref="InvalidOperationException">Controller型の命名規則違反</exception>
        public string GetControllerId<TControllerType>() where TControllerType : Controller
        {
            string controllerIdWithPostfix = typeof(TControllerType).Name;
            if (controllerIdWithPostfix.EndsWith("Controller"))
                return controllerIdWithPostfix.Substring(0, controllerIdWithPostfix.Length - ("Controller".Length));
            throw new InvalidOperationException("Controller classes must end with 'Controller' postfix");
        }

        /// <summary>
        /// コントローラの型から、コントローラのインスタンスを取得する
        /// </summary>
        /// <typeparam name="TControllerType"></typeparam>
        /// <returns></returns>
        public TControllerType GetController<TControllerType>() where TControllerType : Controller
        {
            return _controllers.First(elem => elem is TControllerType) as TControllerType;
        }

        /// <summary>
        /// IDからRegionを取得する
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        internal Region FindRegionByID(string ID)
        {
            return _regions.First(elem => elem.ControllerID == ID);
        }

        /// <summary>
        /// ViewModelを生成する
        /// </summary>
        /// <param name="controllerID"></param>
        /// <param name="pageName"></param>
        /// <param name="navigationMode"></param>
        /// <param name="parameter"></param>
        /// <param name="viewBag"></param>
        /// <returns></returns>
        internal MVVMCViewModel CreateViewModel(string controllerID,
                                                string pageName,
                                                NavigationMode navigationMode,
                                                object parameter,
                                                Dictionary<string, object> viewBag)
        {
            var viewModel = CreateViewModelInstance(controllerID, pageName);
            if (viewModel != null)
            {
                viewModel.NavigatedToMode = navigationMode;
                viewModel.ViewBag = viewBag;
                viewModel.NavigationParameter = parameter;
                viewModel.Initialize();
            }
            return viewModel;
        }

        /// <summary>
        /// ViewModelを生成する
        /// </summary>
        /// <param name="controllerID"></param>
        /// <param name="pageName"></param>
        /// <returns></returns>
        internal MVVMCViewModel CreateViewModelInstance(string controllerID, string pageName)
        {
            var controllerNamespace = GetControllerTypeById(controllerID).Namespace;
            var type = _viewModelTypes.FirstOrDefault(vm =>
                vm.Namespace == controllerNamespace
                && vm.Name.Equals(pageName + "ViewModel", StringComparison.CurrentCultureIgnoreCase));

            if (type == null) return null;

            var instance = Activator.CreateInstance(type);

            var controller = GetController(controllerID);
            var viewModel = instance as MVVMCViewModel;
            viewModel.SetController(controller);
            return viewModel;
        }

        private FrameworkElement CreateViewInstance(string controllerName, string pageName)
        {
            var controllerNamespace = GetControllerTypeById(controllerName).Namespace;
            var type = _viewTypes.FirstOrDefault(vm =>
                vm.Namespace == controllerNamespace
                && vm.Name.Equals(pageName + "View", StringComparison.CurrentCultureIgnoreCase));
            if (type == null)
            {
                throw new Exception($"Navigation failed! Can't find a class {pageName + "View"} in namespace {controllerNamespace}. A UserControl/UIElement by that name should exist.");
            }
            var instance = Activator.CreateInstance(type);
            return instance as FrameworkElement;
        }


        public MVVMCViewModel ExecuteNavigation(string controllerId, string pageName, object parameter, MVVMCViewModel viewModelFromHistory, NavigationMode navigationMode, Dictionary<string, object> viewBag = null)
        {
            //Historyからの遷移だった場合、既存のViewModelを再利用する。そうでない場合は新たなViewModelを生成する
            var viewModel = viewModelFromHistory ?? CreateViewModel(controllerId, pageName, navigationMode, parameter, viewBag);

            //UIスレッドでの実行
            RunOnUIThread(() =>
            {
                //画面遷移前のページ名を取得する
                var prevPage = GetController(controllerId).GetCurrentPageName();

                //新Viewのインスタンスを生成する
                var view = CreateViewInstance(controllerId, pageName);

                //データコンテキストを新ViewModelに設定する。これによりViewとViewModelが紐づく
                view.DataContext = viewModel;

                //Region（画面猟奇）内のコンテンツを新しいViewに更新する。つまり新しい画面が表示されるようになる。
                ChangeContentInRegion(view, controllerId);

                //画面遷移が発生したことを通知する。画面遷移の履歴を記録したり、他の操作をトリガーするのに利用する。
                NavigationOccured?.Invoke(controllerId, prevPage, pageName);
            });
            return viewModel;
        }

        /// <summary>
        /// Region内のコンテンツを変更する
        /// </summary>
        /// <param name="content"></param>
        /// <param name="controllerID"></param>
        private void ChangeContentInRegion(object content, string controllerID)
        {
            //現在のスレッドを取得
            //UIスレッド以外からUIコントロールにアクセスすると例外が発生するため、UIスレッドからのみアクセスできるようにする
            var currentThread = Thread.CurrentThread;

            //ControllerIDからRegionを取得
            Region navArea = FindRegionByID(controllerID);

            //取得したRegion(navArea)のContantを設定
            navArea.Content = content;
        }


        public void RunOnUIThread(Action act)
        {
            //現在のスレッドがUIスレッドであるか判定する
            if (Thread.CurrentThread == _dispatcher.Thread)
                //UIスレッドであればそのまま実行する
                act();
            else
            {
                //UIスレッドでなければ、UIスレッドに処理を投げる
                _dispatcher.Invoke(act);
            }
        }

        /// <summary>
        /// 指定されたActionをUIスレッドで非同期実行させる
        /// </summary>
        /// <param name="act"></param>
        public void RunOnUIThreadAsync(Action act)
        {
            //引数のActionをUIスレッドのディスパッチャに送信する。
            //これにより、UIスレッドがアイドル状態になるタイミングで指定されたアクションを実行する。
            //つまり、UIスレッドが他の作業をしている最中でもUIスレッドが空いた時にActionが実行されることを保証する。
            //この方法を使用することで、UIスレッド上での長時間実行される操作が、UIをフリーズさせることなく実行される。
            _dispatcher.BeginInvoke(act);
        }


        public void AddGoBackCommand(GoBackCommand goBackCommand)
        {
            _goBackCommands.Add(new WeakReference<GoBackCommand>(goBackCommand));
        }

        public void AddGoForwardCommand(GoForwardCommand goForwardCommand)
        {
            _goForwardCommands.Add(new WeakReference<GoForwardCommand>(goForwardCommand));
        }

        public void ChangeCanGoBack(string controllerId)
        {
            //controllerIdに紐づけられた「戻る」コマンドのリストを取得する
            var goBackCommands = GetGoBackCommands(controllerId);
            foreach(var goBackCommand in goBackCommands)
            {
                goBackCommand.ChangeCanExecute();
            }
            CanGoBackChangedEvent?.Invoke(controllerId);
        }

        public void ChangeCanGoForward(string controllerId)
        {
            var goForwardCommands = GetGoForwardCommands(controllerId);
            foreach (var goForwardCommand in goForwardCommands)
            {
                goForwardCommand.ChangeCanExecute();
            }
            CanGoForwardChangedEvent?.Invoke(controllerId);
        }

        public IEnumerable<GoBackCommand> GetGoBackCommands(string controllerId)
        {
            RefreshGoBackCommands();
            return _goBackCommands.Select(wr =>
            {
                wr.TryGetTarget(out GoBackCommand goBackCommand);
                return goBackCommand;
            }).Where(cmd => cmd != null && cmd.ControllerID.Equals(controllerId, StringComparison.CurrentCultureIgnoreCase));
        }

        public IEnumerable<GoForwardCommand> GetGoForwardCommands(string controllerId)
        {
            RefreshGoForwardCommands();
            return _goForwardCommands.Select(wr =>
            {
                wr.TryGetTarget(out GoForwardCommand goForwardCommand);
                return goForwardCommand;
            }).Where(cmd => cmd != null && cmd.ControllerID.Equals(controllerId, StringComparison.CurrentCultureIgnoreCase));
        }

        private void RefreshGoBackCommands()
        {
            List<WeakReference<GoBackCommand>> toRemove =
                _goBackCommands.Where(wr => !wr.TryGetTarget(out GoBackCommand goBackCommand)).ToList();
            foreach (var wr in toRemove)
            {
                _goBackCommands.Remove(wr);
            }
        }

        private void RefreshGoForwardCommands()
        {
            List<WeakReference<GoForwardCommand>> toRemove =
                _goForwardCommands.Where(wr => !wr.TryGetTarget(out GoForwardCommand goForwardCommand)).ToList();
            foreach (var wr in toRemove)
            {
                _goForwardCommands.Remove(wr);
            }
        }

    }
}