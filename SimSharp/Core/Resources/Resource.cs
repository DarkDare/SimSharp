﻿#region License Information
/* SimSharp - A .NET port of SimPy, discrete event simulation framework
Copyright (C) 2014  Heuristic and Evolutionary Algorithms Laboratory (HEAL)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.*/
#endregion

using System;
using System.Collections.Generic;

namespace SimSharp {
  public class Resource {

    public int Capacity { get; protected set; }

    protected Environment Environment { get; private set; }

    protected List<Request> RequestQueue { get; private set; }
    protected List<Release> ReleaseQueue { get; private set; }
    protected List<Request> Users { get; private set; }

    public Resource(Environment environment, int capacity = 1) {
      if (capacity <= 0) throw new ArgumentException("Capacity must > 0.", "capacity");
      Environment = environment;
      Capacity = capacity;
      RequestQueue = new List<Request>();
      ReleaseQueue = new List<Release>();
      Users = new List<Request>();
    }

    public virtual Request Request() {
      var request = new Request(Environment, TriggerRelease, ReleaseCallback);
      RequestQueue.Add(request);
      DoRequest(request);
      return request;
    }

    public virtual Release Release(Request request) {
      var release = new Release(Environment, request, TriggerRequest);
      ReleaseQueue.Add(release);
      DoRelease(release);
      return release;
    }

    protected virtual void ReleaseCallback(Event @event) {
      var request = @event as Request;
      if (request != null) Release(request);
    }

    protected virtual void DoRequest(Request request) {
      if (Users.Count < Capacity) {
        Users.Add(request);
        request.Succeed();
      }
    }

    protected virtual void DoRelease(Release release) {
      if (!release.Request.IsScheduled) RequestQueue.Remove(release.Request);
      Users.Remove(release.Request);
      release.Succeed();
      if (!release.IsScheduled) ReleaseQueue.Remove(release);
    }

    protected virtual void TriggerRequest(Event @event) {
      ReleaseQueue.Remove((Release)@event);
      foreach (var requestEvent in RequestQueue) {
        if (!requestEvent.IsScheduled) DoRequest(requestEvent);
        if (!requestEvent.IsScheduled) break;
      }
    }

    protected virtual void TriggerRelease(Event @event) {
      RequestQueue.Remove((Request)@event);
      foreach (var releaseEvent in ReleaseQueue) {
        if (!releaseEvent.IsScheduled) DoRelease(releaseEvent);
        if (!releaseEvent.IsScheduled) break;
      }
    }
  }
}