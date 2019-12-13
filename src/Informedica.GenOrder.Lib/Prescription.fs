﻿namespace Informedica.GenOrder.Lib

/// Types and functions that presents a `Prescription`, i.e. either a 
/// process, or a continuous prescription or a discontinuous prescription
/// with or without a time
module Prescription =

    open Informedica.GenUnits.Lib
    
    module FR = VariableUnit.Frequency
    module TM = VariableUnit.Time

    /// Type that represents a prescription
    type Prescription = 
        /// A process
        | Process
        /// A continuous infusion
        | Continuous
        /// A discontinuous presciption with a frequency
        | Discontinuous of FR.Frequency
        /// A discontinuous prescription with both frequency and time
        | Timed of FR.Frequency * TM.Time

    /// A `Process`
    let ``process`` = Process

    /// Create `Frequency` and `Time` with name generated by string list **n**
    let freqTime tu1 tu2 n =  (FR.frequency n tu1, TM.time n tu2)

    /// Create a continuous `Prescription` with name generated by string list **n**
    let continuous tu1 tu2 n =  
        let _, _ = n |> freqTime tu1 tu2 in Continuous

    /// Create a discontinuous `Prescription` with name generated by string list **n**
    let discontinuous tu1 tu2 n =  
        let frq, _ = n |> freqTime tu1 tu2 in frq |> Discontinuous
    
    /// Create a timed `Prescription` with name generated by string list **n**
    let timed tu1 tu2 n = 
        let frq, tme = n |> freqTime tu1 tu2 in (frq, tme) |> Timed
    
    /// Check whether a `Presciption` is continuous
    let isContinuous = function | Continuous -> true | _ -> false

    /// Check whether a `Presciption` is discontinuous with a time
    let isTimed = function | Timed _ -> true | _ -> false

    /// Turn `Prescription` **prs** into `VariableUnit`s to
    /// be used in equations
    let toEqs prs =
        match prs with
        | Process    -> None, None
        | Continuous -> None, None
        | Discontinuous (frq) -> 
            frq |> FR.toVarUnt |> Some, None
        | Timed(frq, tme)     -> 
            frq |> FR.toVarUnt |> Some, tme |> TM.toVarUnt |> Some

    /// Set a list of `Equation` **eqs** to a `Prescription` **prs** 
    let fromEqs eqs units prs =
        match prs with
        | Process    -> Process
        | Continuous -> Continuous
        | Discontinuous (frq) -> 
            (frq |> FR.fromVar eqs units) |> Discontinuous
        | Timed(frq, tme) -> 
            (frq |> FR.fromVar eqs units, tme |> TM.fromVar eqs units) |> Timed
        
    /// Turn a `Prescription` **prs** into 
    /// a string list 
    let toString (prs: Prescription) =
            match prs with
            | Process    -> ["Process"]
            | Continuous -> ["Continuous"]
            | Discontinuous (frq) -> [frq |> FR.toString]
            | Timed(frq, tme)     -> [frq |> FR.toString; tme |> TM.toString]
        
                
    module Dto =
        
        module Units = ValueUnit.Units
        module Id = WrappedString.Id
        module NM = WrappedString.Name

        type Dto () =
            member val IsProcess = false with get, set
            member val IsContinuous = false with get, set
            member val IsDiscontinuous = false with get, set
            member val IsTimed = false with get, set
            member val Frequency = VariableUnit.Dto.dto () with get, set
            member val Time = VariableUnit.Dto.dto () with get, set

        let fromDto (dto : Dto) =
            match dto.IsProcess, 
                  dto.IsContinuous, 
                  dto.IsDiscontinuous, 
                  dto.IsTimed with
            | true,  false, false, false -> Process
            | false, true,  false, false -> Continuous
            | false, false, true,  false -> 
                dto.Frequency
                |> FR.fromDto
                |> Discontinuous
            | false, false, false, true  -> 
                (dto.Frequency |> FR.fromDto, dto.Time |> TM.fromDto)
                |> Timed
            | _ -> exn "dto is neither or both process, continuous, discontinuous or timed"
                   |> raise 

        let toDto pres =
            let dto = Dto ()

            match pres with
            | Process -> dto.IsProcess <- true
            | Continuous -> dto.IsContinuous <- true
            | Discontinuous freq ->
                dto.IsDiscontinuous <- true
                dto.Frequency <- freq |> FR.toDto
            | Timed (freq, time) ->
                dto.IsTimed <- true
                dto.Frequency <- freq |> FR.toDto
                dto.Time      <- time |> TM.toDto
                
            dto

        let dto n =
            let dto  = Dto ()
            let f, t = freqTime ValueUnit.NoUnit ValueUnit.NoUnit [ n ]
            
            dto.Frequency <- f |> FR.toDto
            dto.Time <- t |> TM.toDto
            dto.IsProcess <- true

            dto

        let setToProcess (dto : Dto) =
            dto.IsProcess <- true
            dto.IsContinuous <- false
            dto.IsDiscontinuous <- false
            dto.IsTimed <- false
            dto

        let setToContinuous (dto : Dto) =
            dto.IsProcess <- false
            dto.IsContinuous <- true
            dto.IsDiscontinuous <- false
            dto.IsTimed <- false
            dto

        let setToDiscontinuous (dto : Dto) =
            dto.IsProcess <- false
            dto.IsContinuous <- false
            dto.IsDiscontinuous <- true
            dto.IsTimed <- false
            dto

        let setToTimed (dto : Dto) =
            dto.IsProcess <- false
            dto.IsContinuous <- false
            dto.IsDiscontinuous <- false
            dto.IsTimed <- true
            dto