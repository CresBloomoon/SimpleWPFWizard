using MVVMC;

namespace SimpleWPFWizard.Wizard
{
    public class ThirdStepViewModel : MVVMCViewModel
    {
        private int _yearsOfExperience;
        public int YearsOfExperience
        {
            get { return _yearsOfExperience; }
            set
            {
                _yearsOfExperience = value;
                OnPropertyChanged();
            }
        }

        private string _notes;
        public string Notes
        {
            get { return _notes; }
            set
            {
                _notes = value;
                OnPropertyChanged();
            }
        }
    }
}