# ness-backend
Contains Backend

STEP 1 : Install docker - sudo apt install docker.io

STEP 2 : build docker - sudo docker build -t nessefy-backend .

STEP 3 : Run docker - sudo docker run -d -p 5223:5223 nessefy-backend

Note this server is https and reversed proxy to port 5223. 
After ensuring the docker container is running - https://backend.nessefy.com/swagger/index.html


"This file does contain secret keys and access keys at the moment it is public."