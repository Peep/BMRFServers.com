using System;
using System.ServiceModel;
using System.IdentityModel.Selectors;
using System.IdentityModel.Tokens;

namespace Rust 
{
    public class CredentialValidator : UserNamePasswordValidator 
    {
        public override void Validate(string userName, string password) 
        {
            if (userName != "rustful" || password != "lvixqYgxY7saDOdFvq")
            {
                throw new SecurityTokenException("Invalid Username or Password.");
            }
        }
    }
}
