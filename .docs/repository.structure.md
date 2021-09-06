# Repository Structure Guidelines

This repository follows the screaming architecture approach: the repository structure should scream at the user as to what the application does with a minimum amount of effort to understand it.

## Structure Diagram

- root
  - .docs
  - .git
  - .pipeline
    - .scripts *(Any scripts that are ran from within the build or deployment pipeline should reside in this folder)*
    - .templates
  - .vs
  - src
    - app
      - api
        - project
        - tests
      - consumers
        - name
          - project
          - tests
      - ui
        - mobile
          - project
          - tests
        - web
          - project
          - tests
    - libs
      - name
        - project
        - tests
  - .dockerignore
  - .editorconfig
  - azure-pipelines.yml
  - domain.sln *(Replace 'domain' with name of domain)*
  - readme.md
  - .gitattributes
  - .gitignore
  - contributing.md
  - conventional.types.md
  - GitVersion.yml
  - license.md
