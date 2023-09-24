using MVVMC.Enums;
using System.Collections.Generic;

namespace MVVMC
{
    public abstract class MVVMCViewModel : BaseViewModel
    {
        /// <summary>
        /// ViewModelがViewに渡す情報
        /// </summary>
        public Dictionary<string, object> ViewBag { get; set; }
        public object NavigationParameter { get; set; }
        protected IController _controller;

        public NavigationMode NavigatedToMode { get; set; }

        /// <summary>
        /// ViewModelが作成され、NavigationParameterおよびViewBagが設定された後に呼ばれる
        /// </summary>
        public virtual void Initialize() { }

        /// <summary>
        /// Regionが変更され、Viewがすでに読み込まれた後に呼ばれる
        /// </summary>
        public virtual void OnLoad() { }

        /// <summary>
        /// ViewModelからNavigationが離れるときに呼ばれる
        /// </summary>
        /// <param name="args"></param>
        public virtual void OnLeave(LeavingPageEventArgs args) { }

        public virtual void SetController(IController controller)
        {
            _controller = controller;
        }

        public IController GetController()
        {
            return _controller;
        }
    }

    public class MVVMCViewModel<TController> : MVVMCViewModel where TController : Controller
    {
        TController _exactController = null;

        public override void SetController(IController controller)
        {
            _controller = controller;
            _exactController = controller as TController;
        }

        public TController GetExactController()
        {
            return _exactController;
        }
    }
}