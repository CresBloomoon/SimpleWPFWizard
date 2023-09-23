using MVVMC;
using System.Collections.Generic;

namespace SimpleWPFWizard.Wizard
{
    public sealed class Model
    {
        public string Position { get; set; }
        public int YearsOfExperience { get; set; }
        public string Notes { get; set; }
    }

    public class WizardController : Controller
    {
        private Model _model;

        public override void Initial()
        {
            FirstStep();
        }

        private void FirstStep()
        {
            _model = new Model();
            ExecuteNavigation();
        }

        private void SecondStep()
        {
            ExecuteNavigation();
        }

        private void ThirdStep()
        {
            ExecuteNavigation();
        }

        private void FourthStep()
        {
            ExecuteNavigation(null, new Dictionary<string, object>()
            {
                { "Position",_model.Position},
                { "YearsOfExperience",_model.YearsOfExperience.ToString()},
                { "Notes",_model.Notes},

            });
        }

        public void Next()
        {
            if (this.GetCurrentPageName() + "View" == nameof(FirstStepView))
            {
                SecondStep();
            }
            else if (this.GetCurrentViewModel() is SecondStepViewModel secondStepViewModel)
            {
                _model.Position = GetPosition(secondStepViewModel);
                ThirdStep();
            }
            else if (this.GetCurrentViewModel() is ThirdStepViewModel thirdStepViewModel)
            {
                _model.YearsOfExperience = thirdStepViewModel.YearsOfExperience;
                _model.Notes = thirdStepViewModel.Notes;
                FourthStep();

            }
            else // From fourth step
            {
                ClearHistory();
                FirstStep();
            }

        }

        private string GetPosition(SecondStepViewModel secondStepViewModel)
        {
            if (secondStepViewModel.IsQAEngineer)
                return "QA Engineer";
            else if (secondStepViewModel.IsSoftwareEngineer)
                return "Software Engineer";
            else
                return "Team Leader";
        }
    }
}
