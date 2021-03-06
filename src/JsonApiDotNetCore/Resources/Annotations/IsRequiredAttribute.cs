using System;
using System.ComponentModel.DataAnnotations;
using JsonApiDotNetCore.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace JsonApiDotNetCore.Resources.Annotations
{
    /// <summary>
    /// Used with model state validation as a replacement for the built-in <see cref="RequiredAttribute"/> to support partial updates.
    /// </summary>
    public sealed class IsRequiredAttribute : RequiredAttribute
    {
        private bool _isDisabled;

        /// <inheritdoc />
        public override bool IsValid(object value)
        {
            return _isDisabled || base.IsValid(value);
        }

        /// <inheritdoc />
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (validationContext == null) throw new ArgumentNullException(nameof(validationContext));

            var httpContextAccessor = (IHttpContextAccessor)validationContext.GetRequiredService(typeof(IHttpContextAccessor));
            _isDisabled = httpContextAccessor.HttpContext.IsValidatorDisabled(validationContext.MemberName, validationContext.ObjectType.Name);
            return _isDisabled ? ValidationResult.Success : base.IsValid(value, validationContext);
        }
    }
}
