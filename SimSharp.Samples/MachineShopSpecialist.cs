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
using System.Linq;

namespace SimSharp.Samples {
  public class MachineShopSpecialist {
    /*
     * Machine shop specialist example
     * 
     * Covers:
     *  - Resources: ResourcePool
     * 
     * Scenario:
     *  A variant of the machine shop showing how to use individual
     *  resources in the ResourcePool rather than treating them as
     *  a capacity. A workshop has *n* machines. A stream of jobs
     *  (enough to  keep the machines busy) arrives. Each machine
     *  breaks down periodically. Repairs are carried out by two
     *  repairman: Jack can repair machines 1-n/2 faster than John
     *  and vice versa. The workshop works continuously.
     */
    private const int RandomSeed = 42;
    private const double PtMean = 10.0; // Avg. processing time in minutes
    private const double PtSigma = 2.0; // Sigma of processing time
    private const double Mttf = 300.0; // Mean time to failure in minutes
    private const double BreakMean = 1 / Mttf; // Param. for expovariate distribution
    private const double RepairTimeShort = 30.0; // Time it takes to repair a machine in minutes
    private const double RepairTimeLong = 45.0; // Time it takes to repair a machine in minutes
    private const int NumMachines = 10; // Number of machines in the machine shop
    private static readonly TimeSpan SimTime = TimeSpan.FromDays(28); // Simulation time in minutes

    public static double TimePerPart(Random random) {
      // Return actual processing time for a concrete part.
      return RandomDist.Normal(random, PtMean, PtSigma);
    }

    public static double TimeToFailure(Random random) {
      // Return time until next failure for a machine.
      return RandomDist.Exponential(random, BreakMean);
    }

    private static readonly object Jack = new object();
    private static readonly object John = new object();

    private class Machine : ActiveObject<Environment> {
      /*
       * A machine produces parts and my get broken every now and then.
       * If it breaks, it requests a *repairman* and continues the production
       * after the it is repaired.
       * 
       *  A machine has a *name* and a numberof *parts_made* thus far.
       */
      public string Name { get; private set; }
      public int Index { get; private set; }
      public int PartsMade { get; private set; }
      public bool Broken { get; private set; }
      public Process Process { get; private set; }

      public Machine(Environment env, int idx, ResourcePool repairman)
        : base(env) {
        Index = idx;
        Name = "Machine " + idx;
        PartsMade = 0;
        Broken = false;

        // Start "working" and "break_machine" processes for this machine.
        Process = env.Process(Working(repairman));
        env.Process(BreakMachine());
      }

      private IEnumerable<Event> Working(ResourcePool repairman) {
        /*
         * Produce parts as long as the simulation runs.
         * 
         * While making a part, the machine may break multiple times.
         * Request a repairman when this happens.
         */
        while (true) {
          // Start making a new part
          var doneIn = TimeSpan.FromMinutes(TimePerPart(Environment.Random));
          while (doneIn > TimeSpan.Zero) {
            // Working on the part
            var start = Environment.Now;
            yield return Environment.Timeout(doneIn);
            if (Environment.ActiveProcess.HandleFault()) {
              Broken = true;
              doneIn -= Environment.Now - start;
              // How much time left?
              // Request a repairman. This will preempt its "other_job".
              Func<object, bool> getSpecialist = x => Index < NumMachines / 2 ? x == Jack : x == John;
              var specialistAvailable = repairman.IsAvailable(getSpecialist);
              using (var req = repairman.Request(specialistAvailable ? getSpecialist : null)) {
                yield return req;
                var repairTime = getSpecialist(req.Value)
                  ? TimeSpan.FromMinutes(RepairTimeShort)
                  : TimeSpan.FromMinutes(RepairTimeLong);
                //Environment.Log((req.Value == Jack ? "Jack" : "John") + " is working on " + Name + " for " + repairTime.Minutes + " minutes.");
                yield return Environment.Timeout(repairTime);
              }
              Broken = false;
            } else {
              doneIn = TimeSpan.Zero; // Set to 0 to exit while loop.
            }
          }
          // Part is done.
          PartsMade++;
        }
      }

      private IEnumerable<Event> BreakMachine() {
        // Break the machine every now and then.
        while (true) {
          yield return Environment.Timeout(TimeSpan.FromMinutes(TimeToFailure(Environment.Random)));
          if (!Broken) {
            // Only break the machine if it is currently working.
            Process.Interrupt();
          }
        }
      }
    }

    public void Simulate(int rseed = RandomSeed) {
      // Setup and start the simulation
      // Create an environment and start the setup process
      var start = new DateTime(2014, 2, 1);
      var env = new Environment(start, rseed);
      env.Log("== Machine shop specialist ==");
      var repairman = new ResourcePool(env, new[] { Jack, John });
      var machines = Enumerable.Range(0, NumMachines).Select(x => new Machine(env, x, repairman)).ToArray();

      // Execute!
      env.Run(SimTime);

      // Analyis/results
      env.Log("Machine shop results after {0} days.", (env.Now - start).TotalDays);
      foreach (var machine in machines)
        env.Log("{0} made {1} parts.", machine.Name, machine.PartsMade);
    }
  }
}