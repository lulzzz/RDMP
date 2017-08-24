using BrightIdeasSoftware;

namespace ReusableUIComponents.Copying
{
    public interface ICommandExecutionFactory
    {
        /// <summary>
        /// Creates an ICommandExecution which reflects the combining of the two objects (ICommand can even reflect a collection).  If no possible combination of the two objects is possible
        /// then null is returned.  If the two objects are theoretically usable with one another but the state of the one or other is illegal then an ICommandExecution will be returned by the
        /// IsImpossible/ReasonCommandImpossible flags will be set and it will (should!) crash if run.
        /// 
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="targetModel"></param>
        /// <returns></returns>
        ICommandExecution Create(ICommand cmd, object targetModel,InsertOption insertOption = InsertOption.Default);
    }

    public enum InsertOption
    {
        Default,
        InsertAbove,
        InsertBelow
    }
}