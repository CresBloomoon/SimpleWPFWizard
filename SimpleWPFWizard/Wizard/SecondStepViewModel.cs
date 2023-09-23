using MVVMC;

namespace SimpleWPFWizard.Wizard
{
    public class SecondStepViewModel : MVVMCViewModel
    {
        private bool _isQAEngineer;
        public bool IsQAEngineer
        {
            get { return _isQAEngineer; }
            set
            {
                _isQAEngineer = value;
                OnPropertyChanged();
            }
        }

        private bool _isSoftwareEngineer;
        public bool IsSoftwareEngineer
        {
            get { return _isSoftwareEngineer; }
            set
            {
                _isSoftwareEngineer = value;
                OnPropertyChanged();
            }
        }

        private bool _isTeamLeader;
        public bool IsTeamLeader
        {
            get { return _isTeamLeader; }
            set
            {
                _isTeamLeader = value;
                OnPropertyChanged();
            }
        }
    }
}