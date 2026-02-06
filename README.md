# PGraphInCS
A flexible implementation of P-Graph algorithms in C#.

Current release version: 1.0.0 (Also available in NuGet under the same name: PGraphInCS)

## Description

The aim of the library is to provide the code-base for quick and flexible implementation of P-graph algorithms, while maintaining acceptable performance. The library contains implementations of the main algorithms of the P-graph framework, i.e., MSG, SSG, and ABB. It provides several configurable base classes and implementations of branch-and-bound algorithms. This allows new algorithms to be developed both for the existing and for extended problems. When creating new algorithms, only the new logic has to be implemented, while the unchanged parts can be used directly from this library. Also contains compatibility with the main problems supported by P-Graph Studio.

## Examples and tutorials

The examples folder contains sample projects that serve as tutorials. The samples each discuss certain parts and functionalities of the library.

The library does not explain the P-graph framework, nor its main algorithms. It is reasonable to expect that anyone interested in this library is already adequately familiar with the P-graph framework.

Current examples:  
- Example 1 (Basics): This example is the starting point, explaining the basic elements of the library as well as introducing algorithms MSG and SSG.
- Example 2 (Branch and Bound): Explains the library's branch-and-bound capabilities and options. Contains lots of text and explanation, but important to understand the basic operation of the library.
- Three examples showcase the three supported ways to add additional data the the PNS problem. Choose one or combine more the extend your data:
  - Example 3A (The quick way): Explains the quickest way to extend the data, in case there are only some small things to add. Suggested for smaller changes.
  - Example 3B (The efficient way): Explains the efficient way to extend the data, prioritizing type safety, code readibility, and computational efficiency. Suggested for larger changes.
  - Example 3C (The flexible way): Explains the flexible way to extend the data, focusing on reusability of components for multiple problems. Suggested if you expect to solve multiple problems of the same materials and operating units while varying the new data.
- The library provides compatibility with the main model of P-Graph Studio for convenience. It is available in both efficient-style and flexible-style implementation:
  - Example 4A (Linear PNS the efficient way): Showcases how to use the efficient implementation of the linear model.
  - Example 4B (Linear PNS the flexible way): Showcases how to use the flexible implementation of the linear model.
- Example 5 (Custom bounding): Explains the role of the bounding methods in more detail
- Further examples will arrive soon...
