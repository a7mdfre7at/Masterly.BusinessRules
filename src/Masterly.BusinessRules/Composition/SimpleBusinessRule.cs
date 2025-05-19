using System;

namespace Masterly.BusinessRules
{
    public class SimpleBusinessRule : BaseBusinessRule
    {
        private readonly Func<bool> _isBrokenFunc;
        private readonly string _message;
        private readonly string _code;

        public SimpleBusinessRule(Func<bool> isBrokenFunc, string message, string code = "SIMPLE")
        {
            _isBrokenFunc = isBrokenFunc;
            _message = message;
            _code = code;
        }

        public override bool IsBroken() => _isBrokenFunc();
        public override string Message => _message;
        public override string Code => _code;
    }
}