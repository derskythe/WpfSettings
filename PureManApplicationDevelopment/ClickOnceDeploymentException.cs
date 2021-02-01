using System;

namespace PureManApplicationDeployment
{
    public class ClickOnceDeploymentException : Exception
    {
        public ClickOnceDeploymentException(string message) : base(message)
        {
        }
    }
}
