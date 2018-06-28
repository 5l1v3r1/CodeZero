﻿//  <copyright file="OnlineUserEventArgs.cs" project="CodeZero" solution="CodeZero">
//      Copyright (c) 2018 CodeZero Framework.  All rights reserved.
//  </copyright>
//  <author>Nasr Aldin M.</author>
//  <email>nasr2ldin@gmail.com</email>
//  <website>https://nasraldin.com/codezero</website>
//  <github>https://nasraldin.github.io/CodeZero</github>
//  <date>01/01/2018 01:00 AM</date>
namespace CodeZero.RealTime
{
    public class OnlineUserEventArgs : OnlineClientEventArgs
    {
        public UserIdentifier User { get; }

        public OnlineUserEventArgs(UserIdentifier user,IOnlineClient client) 
            : base(client)
        {
            User = user;
        }
    }
}