Jenkins Setup
======================

1. Review the storage for Jenkins.  If it is only 1GB in size, Create new storage for Jenkins.  Preferably 5 GB of Gluster-File, as Gluster-Block is problematic.
2. Adjust the deployment environment for Jenkins to resolve known issues :
  2.1 Set `JAVA_OPTS` to `-XX:MaxMetaspaceSize=512m -Dhudson.model.DirectoryBrowserSupport.CSP=`
  2.2 Wait for the deployment to complete.
3. Login to Jenkins as Admin
4. Add a Kubernetes Pod Template for BDDStack
4.1 Go to Manage Jenkins
4.2 Click on Configure Jenkins
4.3 Add a new Kubernetes Pod Template for BDDStack (functional testing using Geb and Spock)
4.3.1 Name:  bddstack
4.3.2 Labels: bddstack
4.3.3 Name of pod template to inherit from - leave blank
4.3.4 Container Template:
4.3.4.1 Name:  jnlp
4.3.4.1 Docker image: 172.50.0.2:5000/openshift/jenkins-slave-bddstack  (substitute 172.50.0.2:5000 for the address of your OpenShift registry)
4.3.4.2 Always pull image:  checked
4.3.4.3 Working directory: /home/jenkins
4.3.4.4 Command to run:  leave blank
4.3.4.5 Arguments to pass to the command:  ${computer.jnlpmac} ${computer.name}
4.3.4.6 Allocate pseudo-TTY: checked
4.3.4.7 Advanced
4.3.4.7.1 Request CPU:  500m
4.3.4.7.2 Request Memory: 3Gi
4.3.4.7.3 Limit CPU: 1000m
4.3.4.7.4 Limit Memory: 4Gi
4.3.4.7.5 Liveness Probe Exec action: Leave Blank
4.3.4.7.6 All other fields - set to 0.  No PortMappings.
4.3.4.8 Set Timeout in seconds for Jenkins connection to 100  (If you don't see this field, do not worry about it)

4.4 Add a new Kubernetes Pod Template for ZAP (Zed Attack Proxy)
4.4.1 Name:  zap
4.4.2 Labels: zap
4.4.3 Name of pod template to inherit from - leave blank
4.4.4 Container Template:
4.4.4.1 Name:  jnlp
4.4.4.1 Docker image: 172.50.0.2:5000/openshift/jenkins-slave-zap (substitute 172.50.0.2:5000 for the address of your OpenShift registry)
4.4.4.2 Always pull image:  not checked
4.4.4.3 Working directory: /home/jenkins
4.4.4.4 Command to run:  leave blank
4.4.4.5 Arguments to pass to the command:  ${computer.jnlpmac} ${computer.name}
4.4.4.6 Allocate pseudo-TTY: checked
4.4.4.7 Set Max number to 1 (Max number of instances to create) 
4.4.4.7 Advanced
4.4.4.7.1 Request CPU:  100m
4.4.4.7.2 Request Memory: 2Gi
4.4.4.7.3 Limit CPU: 500m
4.4.4.7.4 Limit Memory: 3Gi
4.4.4.7.5 Liveness Probe Exec action: Leave Blank
4.4.4.7.6 All other fields - set to 0.  No PortMappings.
4.4.4.8 Set Timeout in seconds for Jenkins connection to 100  (If you don't see this field, do not worry about it)

5. Clear the Kubernetes Container Cap field (Replace 10 with blank)

6.  If you have slow running builds that may exceed any of the OpenShift timeouts, increase the timeout appropriately.  Current GCPE builds should not require a change to the timeouts.

7. Save changes to Jenkins Config. 

8.  Verify that builds work.