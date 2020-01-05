﻿namespace Informedica.GenOrder.Lib

module ValueRange =

    open Informedica.GenUnits.Lib
    open Informedica.GenSolver.Lib.Variable.ValueRange

    module HashSet = Informedica.GenSolver.Lib.HashSet

    /// Convert a `ValueRange` to a `string`.
    let toStringWithUnit un vr =
        let fVs vs = 
            let vs = 
                vs 
                |> HashSet.toList
                |> List.map (ValueUnit.create un)
                |> List.map ValueUnit.toUnit

            print false vs None false [] None false
    
        let some =
            ValueUnit.create un
            >> ValueUnit.toUnit
            >> Some

        let fRange =
            let print min minincl incr max maxincl = 
                print false [] min minincl incr max maxincl

            let fMin min =
                let min, minincl = 
                    match min with
                    | MinIncl v -> v |> some, true
                    | MinExcl v -> v |> some, false  
                print min minincl [] None false

            let fMax max =
                let max, maxincl = 
                    match max with
                    | MaxIncl v -> v |> some, true
                    | MaxExcl v -> v |> some ,false  

                print None false [] max maxincl

            let fMinIncr (min, incr)  = 
                let min, minincl = 
                    match min with
                    | MinIncl v -> v |> some, true
                    | MinExcl v -> v |> some ,false  

                let incr = incr |> incrToValue |> HashSet.toList
        
                print min minincl incr None false

            let fIncrMax (incr, max)  = 
                let max, maxincl = 
                    match max with
                    | MaxIncl v -> v |> some, true
                    | MaxExcl v -> v |> some ,false  

                let incr = incr |> incrToValue |> HashSet.toList
        
                print None false incr max maxincl

            let fMinMax (min, max) =
                let min, minincl = 
                    match min with
                    | MinIncl v -> v |> some, true
                    | MinExcl v -> v |> some ,false  

                let max, maxincl = 
                    match max with
                    | MaxIncl v -> v |> some, true
                    | MaxExcl v -> v |> some ,false  

                print min minincl [] max maxincl

            applyRange fMin fMax fMinIncr fIncrMax fMinMax

        let unr = print true [] None false [] None false
    
        vr |> apply unr fVs fRange 


