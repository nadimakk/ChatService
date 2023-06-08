# ChatService

## About
This is the course project of **EECE 503E Web Services in the Cloud** offered by Professor [Nehme Bilal](https://github.com/nehmebilal)
at the American University of Beirut (AUB).

It is the backend of a chat application where users can create profiles with profile pictures, create conversations and send messages.

## Acknowledgement
Our profound gratitude goes to Professor Nehme Bilal for his invaluable support, patience, time and guidance to help 
us complete this project. Professor Bilal extensively reviewed our code and made sure we correctly applied the concepts 
he taught us in EECE 503E.

## Goals Achieved
  * implementing the required functionalities using a clean architecture
  * writing SOLID & DRY code
  * fully covering the code with unit tests and integration tests
  * configuring CI and CD through GitHubActions
  * deploying to Microsoft Azure
  
## Technologies
  * ASP.NET Core 6.0
  * xUnit
  * Moq
  * GitHub Actions
  * Microsoft Azure
      * App Service
      * Cosmos DB
      * Blob Storage
      * Application Insights

## Functionalities
The webapp provides RESTful endpoints for:
  * creating a profile
  * fetching a profile by username
  * uploading a profile picture
  * fetching a profile picture by id
  * starting a conversation
  * enumerating conversations of a given user sorted by modified time
  * posting a message to a conversation
  * enumerating messages in a conversation sorted by sending time

<p align="center">
  <img src="https://github.com/nadimakk/ChatService/blob/main/endpoints.png" alt="endpoints">
</p>

## Developers
  * [Nadim Akkaoui](https://github.com/nadimakk)
  * [Ali Jaafar](https://github.com/AliJaafar21)
