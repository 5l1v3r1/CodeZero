//  <copyright file="NullTenantStore.cs" project="CodeZero" solution="CodeZero">
//      Copyright (c) 2018 CodeZero Framework.  All rights reserved.
//  </copyright>
//  <author>Nasr Aldin M.</author>
//  <email>nasr2ldin@gmail.com</email>
//  <website>https://nasraldin.com/codezero</website>
//  <github>https://nasraldin.github.io/CodeZero</github>
//  <date>01/01/2018 01:00 AM</date>
namespace CodeZero.MultiTenancy
{
    public class NullTenantStore : ITenantStore
    {
        public TenantInfo Find(int tenantId)
        {
            return null;
        }

        public TenantInfo Find(string tenancyName)
        {
            return null;
        }
    }
}