﻿/*
 * TfsBot - http://github.com/kria/TfsBot
 * 
 * Copyright (C) 2014 Kristian Adrup
 * 
 * This file is part of TfsBot.
 * 
 * TfsBot is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by the
 * Free Software Foundation, either version 3 of the License, or (at your
 * option) any later version. See included file COPYING for details.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace DevCore.TfsBot.BotServiceContract
{
    [ServiceContract]
    public interface IBotService
    {
        [OperationContract]
        void SendMessage(string message);
    }
}