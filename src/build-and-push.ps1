$ErrorActionPreference = "Stop"

docker build -t mthalman/dockersub:1.0 .
docker push mthalman/dockersub:1.0